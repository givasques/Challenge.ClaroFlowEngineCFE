using System.Diagnostics;
using System.Text.Json;
using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Services;

public interface ICustomerSummaryService
{
    Task<CustomerSummaryResponse> GenerateAsync(Guid customerId, bool forceRefresh, CancellationToken cancellationToken);
}

/// <summary>
/// Orquestra o resumo do cliente (FASE 4.4, A.6 a A.8): checa o cliente, consulta o cache, aplica o limite por
/// atendente, chama o provedor com fallback, guarda só o que veio da IA e audita sem o conteúdo.
/// </summary>
public class CustomerSummaryService : ICustomerSummaryService
{
    private const string AiSource = "ai";
    private const string RulesSource = "rules";
    private const string FallbackLabelText = "IA indisponível no momento";

    private readonly CfeDbContext _db;
    private readonly ICustomerHistoryCollector _collector;
    private readonly IEnumerable<ICustomerSummaryProvider> _providers;
    private readonly IAiSummaryRateLimiter _rateLimiter;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly ICurrentPanelUserAccessor _currentPanelUser;
    private readonly AiSummaryOptions _options;
    private readonly ILogger<CustomerSummaryService> _logger;

    public CustomerSummaryService(
        CfeDbContext db,
        ICustomerHistoryCollector collector,
        IEnumerable<ICustomerSummaryProvider> providers,
        IAiSummaryRateLimiter rateLimiter,
        ITransitionRecorder transitionRecorder,
        ICurrentPanelUserAccessor currentPanelUser,
        IOptions<AiSummaryOptions> options,
        ILogger<CustomerSummaryService> logger)
    {
        _db = db;
        _collector = collector;
        _providers = providers;
        _rateLimiter = rateLimiter;
        _transitionRecorder = transitionRecorder;
        _currentPanelUser = currentPanelUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CustomerSummaryResponse> GenerateAsync(Guid customerId, bool forceRefresh, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Cliente {customerId} não encontrado.");

        if (customer.AnonymizedAt is not null)
            throw new ConflictException("already_anonymized", "Cliente anonimizado: não há resumo a gerar.");

        var history = await _collector.CollectAsync(customerId, cancellationToken);
        var panelUserId = _currentPanelUser.UserId ?? Guid.Empty;

        if (!forceRefresh)
        {
            var cached = await FindCachedAsync(customerId, history.Fingerprint, cancellationToken);
            if (cached is not null)
            {
                // Resposta do cache não consome o limite do atendente (A.6).
                Audit(cached.Source, cached.Model, cached: true, fallbackReason: null);
                LogGeneration(customerId, panelUserId, cached.Source, cached.Model, cached: true, started, fallbackReason: null, inputTokens: null, outputTokens: null, reasoningTokens: null);
                return ToResponse(cached.Content, cached.Source, cached.Model, cached.GeneratedAt, cached: true, fallbackReason: null);
            }
        }

        if (!_rateLimiter.TryAcquire(panelUserId))
            throw new TooManyRequestsException("too_many_requests", "Muitas solicitações de resumo. Aguarde um minuto.");

        var (result, fallbackReason) = await GenerateWithFallbackAsync(history.Portrait, cancellationToken);
        var generatedAt = DateTime.UtcNow;

        // Só resumo da IA é guardado. Resumo por regras é recalculado, e a próxima chamada tenta a IA de novo (A.7).
        if (result.Source == AiSource)
            await StoreAsync(customerId, history.Fingerprint, result, generatedAt, cancellationToken);

        Audit(result.Source, result.Model, cached: false, fallbackReason);
        await _db.SaveChangesAsync(cancellationToken);

        LogGeneration(customerId, panelUserId, result.Source, result.Model, cached: false, started, fallbackReason, result.InputTokens, result.OutputTokens, result.ReasoningTokens);
        return ToResponse(result.Content, result.Source, result.Model, generatedAt, cached: false, fallbackReason);
    }

    private async Task<(CustomerSummaryResult Result, string? FallbackReason)> GenerateWithFallbackAsync(
        CustomerPortrait portrait, CancellationToken cancellationToken)
    {
        var rules = _providers.First(p => p.Name == RuleBasedSummaryProvider.ProviderName);

        // Cliente sem jornada não tem o que resumir: resposta fixa por regras, sem chamar o modelo (A.5).
        if (portrait.Counts.Total == 0 || !_options.Enabled || _options.Provider == RuleBasedSummaryProvider.ProviderName)
            return (await rules.GenerateAsync(portrait, cancellationToken), null);

        var provider = _providers.FirstOrDefault(p => p.Name == _options.Provider);
        if (provider is null || !_options.IsRemoteProviderConfigured())
            return (await rules.GenerateAsync(portrait, cancellationToken), "ai_not_configured");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            return (await provider.GenerateAsync(portrait, timeout.Token), null);
        }
        catch (AiProviderException ex)
        {
            // ex.Message traz só o motivo da falha (ex.: campo ausente, padrão de telefone), nunca o conteúdo da resposta.
            _logger.LogWarning("Resumo com IA indisponível ({FallbackReason}): {FailureDetail}", ex.Reason, ex.Message);
            return (await rules.GenerateAsync(portrait, cancellationToken), ex.Reason);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Resumo com IA excedeu {TimeoutSeconds} s; usando resumo por regras", _options.TimeoutSeconds);
            return (await rules.GenerateAsync(portrait, cancellationToken), "ai_timeout");
        }
        catch (Exception ex)
        {
            // Sem a mensagem da exceção no log: ela pode carregar detalhes de resposta do provedor.
            _logger.LogError("Falha inesperada no provedor de resumo {Provider}: {ExceptionType}", _options.Provider, ex.GetType().Name);
            return (await rules.GenerateAsync(portrait, cancellationToken), "ai_error");
        }
    }

    private async Task<CachedSummary?> FindCachedAsync(Guid customerId, string fingerprint, CancellationToken cancellationToken)
    {
        var row = await _db.CustomerAiSummaries.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.InputFingerprint == fingerprint && s.Source == AiSource)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var content = JsonSerializer.Deserialize<CustomerSummaryContent>(JsonSerializer.Serialize(row.Content));
        return content is null ? null : new CachedSummary(content, row.Source, row.Model, row.GeneratedAt);
    }

    private async Task StoreAsync(Guid customerId, string fingerprint, CustomerSummaryResult result, DateTime generatedAt, CancellationToken cancellationToken)
    {
        // Mantém uma linha de IA por cliente: a nova substitui as anteriores.
        var previous = await _db.CustomerAiSummaries
            .Where(s => s.CustomerId == customerId && s.Source == AiSource)
            .ToListAsync(cancellationToken);
        _db.CustomerAiSummaries.RemoveRange(previous);

        var content = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(result.Content)) ?? new();
        _db.CustomerAiSummaries.Add(new CustomerAiSummary
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            InputFingerprint = fingerprint,
            Source = AiSource,
            Model = result.Model,
            Content = content,
            GeneratedAt = generatedAt,
        });
    }

    /// <summary>Transição órfã de auditoria, sem o conteúdo do resumo (A.8).</summary>
    private void Audit(string source, string? model, bool cached, string? fallbackReason)
    {
        _transitionRecorder.Record(
            journeyContextId: null,
            channel: Channels.Panel,
            eventType: TransitionEventTypes.CustomerAiSummaryGenerated,
            description: "Resumo do cliente gerado para o atendimento.",
            metadata: PanelUserMetadata.Merge(
                new { source, model, cached, fallback_reason = fallbackReason },
                _currentPanelUser));
    }

    /// <summary>Log estruturado sem chave, prompt nem resposta do modelo (A.8).</summary>
    private void LogGeneration(
        Guid customerId, Guid panelUserId, string source, string? model, bool cached, long started,
        string? fallbackReason, int? inputTokens, int? outputTokens, int? reasoningTokens)
    {
        var latencyMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _logger.LogInformation(
            "Resumo do cliente {CustomerId} para o atendente {PanelUserId}: origem {Source}, modelo {Model}, cache {Cached}, latência {LatencyMs} ms, tokens entrada {InputTokens} saída {OutputTokens} raciocínio {ReasoningTokens}, fallback {FallbackReason}",
            customerId, panelUserId, source, model ?? "-", cached, Math.Round(latencyMs), inputTokens ?? 0, outputTokens ?? 0, reasoningTokens ?? 0, fallbackReason ?? "-");
    }

    private static CustomerSummaryResponse ToResponse(
        CustomerSummaryContent content, string source, string? model, DateTime generatedAt, bool cached, string? fallbackReason) =>
        new(
            Summary: content.Summary,
            AttentionPoints: content.AttentionPoints,
            SuggestedApproach: content.SuggestedApproach,
            Source: source,
            SourceLabel: source == AiSource ? "Gerado por IA" : "Resumo automático por regras",
            Model: model,
            GeneratedAt: generatedAt,
            Cached: cached,
            FallbackReason: fallbackReason,
            FallbackLabel: fallbackReason is null ? null : FallbackLabelText);

    private sealed record CachedSummary(CustomerSummaryContent Content, string Source, string? Model, DateTime GeneratedAt);
}
