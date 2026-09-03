using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Opportunities.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Opportunities.Services;

/// <summary>Orquestração do módulo de Oportunidades (FASE 3.6): detecção, listagem e ciclo de vida.</summary>
public class OpportunitiesService : IOpportunitiesService
{
    private const string MockAttendantId = "atendente_painel";
    private const int MaxNotesLength = 500;

    private readonly CfeDbContext _db;
    private readonly IOpportunityDetectorService _detector;
    private readonly ICurrentChannelAccessor _currentChannel;

    public OpportunitiesService(CfeDbContext db, IOpportunityDetectorService detector, ICurrentChannelAccessor currentChannel)
    {
        _db = db;
        _detector = detector;
        _currentChannel = currentChannel;
    }

    public async Task<DetectOpportunitiesResponse> DetectAsync(CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        var detectedAt = DateTime.UtcNow;
        var created = await _detector.DetectAllAsync(cancellationToken);

        return new DetectOpportunitiesResponse(detectedAt, created, created.Values.Sum());
    }

    public async Task<OpportunitiesListResponse> ListAsync(
        string? statusFilter, string? category, string? urgency, int limit, int offset, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        var statuses = string.IsNullOrWhiteSpace(statusFilter)
            ? [OpportunityStatus.New, OpportunityStatus.Contacted]
            : statusFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var now = DateTime.UtcNow;

        var query = _db.Opportunities
            .AsNoTracking()
            .Where(o => statuses.Contains(o.Status) && o.ValidUntil >= now);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(o => o.Category == category);

        if (!string.IsNullOrWhiteSpace(urgency))
            query = query.Where(o => o.Urgency == urgency);

        // Dataset pequeno no protótipo: traz tudo que passou nos filtros e ordena por urgência em
        // memória (Postgres não tem CASE WHEN natural via LINQ pra essa ordenação customizada sem
        // FromSqlRaw), depois pagina.
        var candidates = await query
            .Include(o => o.Customer)
            .Include(o => o.TriggeringJourney)
            .ToListAsync(cancellationToken);

        var total = candidates.Count;

        var page = candidates
            .OrderBy(o => OpportunityUrgency.RankOf(o.Urgency))
            .ThenByDescending(o => o.DetectedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();

        var customerIds = page.Select(o => o.CustomerId).Distinct().ToList();
        var phoneByCustomer = await GetPhonesAsync(customerIds, cancellationToken);

        var dtos = page.Select(o => ToDto(o, phoneByCustomer.GetValueOrDefault(o.CustomerId))).ToList();

        return new OpportunitiesListResponse(total, dtos);
    }

    public Task<OpportunityDto> MarkAsContactedAsync(Guid id, string? notes, CancellationToken cancellationToken) =>
        TransitionAsync(id, OpportunityStatus.New, OpportunityStatus.Contacted, notes, isResolution: false, cancellationToken);

    public Task<OpportunityDto> MarkAsConvertedAsync(Guid id, string? notes, CancellationToken cancellationToken) =>
        TransitionAsync(id, OpportunityStatus.Contacted, OpportunityStatus.Converted, notes, isResolution: true, cancellationToken);

    public Task<OpportunityDto> MarkAsNotRelevantAsync(Guid id, string? notes, CancellationToken cancellationToken) =>
        TransitionAsync(id, OpportunityStatus.Contacted, OpportunityStatus.NotRelevant, notes, isResolution: true, cancellationToken);

    private async Task<OpportunityDto> TransitionAsync(
        Guid id, string requiredCurrentStatus, string newStatus, string? notes, bool isResolution, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();
        ValidateNotes(notes);

        var opportunity = await _db.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.TriggeringJourney)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("opportunity_not_found", $"Oportunidade {id} não encontrada.");

        if (opportunity.Status != requiredCurrentStatus)
        {
            throw new ConflictException(
                "invalid_status_transition",
                $"Oportunidade está no estado '{opportunity.Status}', esperado '{requiredCurrentStatus}'.",
                new { status = opportunity.Status });
        }

        var now = DateTime.UtcNow;
        opportunity.Status = newStatus;

        if (isResolution)
        {
            opportunity.ResolvedAt = now;
            if (!string.IsNullOrWhiteSpace(notes)) opportunity.ResolutionNotes = notes;
        }
        else
        {
            opportunity.ContactedAt = now;
            opportunity.ContactedBy = MockAttendantId;
            if (!string.IsNullOrWhiteSpace(notes)) opportunity.ResolutionNotes = notes;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var phone = (await GetPhonesAsync([opportunity.CustomerId], cancellationToken)).GetValueOrDefault(opportunity.CustomerId);
        return ToDto(opportunity, phone);
    }

    private void EnsurePanelChannel()
    {
        if (_currentChannel.Channel != Channels.Panel)
            throw new ForbiddenException("channel_not_allowed", "Esta ação só pode ser executada pelo painel do atendente.");
    }

    private static void ValidateNotes(string? notes)
    {
        if (notes is { Length: > MaxNotesLength })
            throw new ValidationException("invalid_notes", $"notes deve ter no máximo {MaxNotesLength} caracteres.");
    }

    private async Task<Dictionary<Guid, string?>> GetPhonesAsync(List<Guid> customerIds, CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0) return new();

        var phones = await _db.IdentityLinks
            .AsNoTracking()
            .Where(l => customerIds.Contains(l.CustomerId) && l.Channel == Channels.Whatsapp)
            .Select(l => new { l.CustomerId, l.Identifier })
            .ToListAsync(cancellationToken);

        return phones.ToDictionary(p => p.CustomerId, p => (string?)p.Identifier);
    }

    private static OpportunityDto ToDto(Opportunity o, string? phone)
    {
        var abandonedAtStep = o.Metadata.GetValueOrDefault("abandoned_at_step")?.ToString();
        var triggeringJourney = o.TriggeringJourney is null
            ? null
            : new OpportunityTriggeringJourneyDto(o.TriggeringJourney.Id, o.TriggeringJourney.Intent, abandonedAtStep);

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((o.ValidUntil - DateTime.UtcNow).TotalDays));

        return new OpportunityDto(
            o.Id,
            new OpportunityCustomerDto(o.Customer.Id, o.Customer.FullName, o.Customer.Cpf, phone),
            o.Category,
            OpportunityCategory.Label(o.Category),
            o.Urgency,
            OpportunityUrgency.Label(o.Urgency),
            o.Status,
            o.DetectedAt,
            o.ValidUntil,
            daysRemaining,
            triggeringJourney,
            o.Metadata,
            OpportunityCategory.SuggestedAction(o.Category),
            o.ContactedAt,
            o.ContactedBy,
            o.ResolvedAt,
            o.ResolutionNotes);
    }
}
