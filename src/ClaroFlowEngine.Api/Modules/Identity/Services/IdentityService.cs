using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Identity.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly CfeDbContext _db;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(CfeDbContext db, ILogger<IdentityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResolveIdentityResponse> ResolveAsync(ResolveIdentityRequest request, CancellationToken cancellationToken)
    {
        ValidateChannelAndIdentifier(request.Channel, request.Identifier);
        if (request.CpfHint is not null && !IdentifierFormat.IsValidCpf(request.CpfHint))
            throw new ValidationException("invalid_cpf", "CPF inválido — verifique os dígitos digitados.");

        var existingLink = await FindLinkAsync(request.Channel, request.Identifier, cancellationToken);
        if (existingLink is not null)
        {
            _logger.LogInformation(
                "Identity resolved via existing link for channel {Channel}", request.Channel);
            return ToResponse(existingLink.Customer, wasCreated: false, request.Channel, request.Identifier);
        }

        // Canal "cpf": o próprio identificador é o CPF a ser buscado/vinculado.
        if (request.Channel == Common.Contracts.Channels.Cpf)
        {
            return await ResolveByCpfAsync(
                cpf: IdentifierFormat.SanitizeCpf(request.Identifier),
                linkChannel: request.Channel,
                linkIdentifier: request.Identifier,
                fullNameHint: request.FullNameHint,
                cancellationToken: cancellationToken);
        }

        // Outros canais: só é possível resolver/criar identidade se um CPF foi informado como dica.
        if (string.IsNullOrWhiteSpace(request.CpfHint))
        {
            throw new NotFoundException(
                "identity_not_found",
                "Identidade não encontrada para este canal. Informe um CPF (cpf_hint) para vinculação.");
        }

        return await ResolveByCpfAsync(
            cpf: IdentifierFormat.SanitizeCpf(request.CpfHint),
            linkChannel: request.Channel,
            linkIdentifier: request.Identifier,
            fullNameHint: request.FullNameHint,
            cancellationToken: cancellationToken);
    }

    public async Task<ResolveIdentityResponse> GetAsync(string channel, string identifier, CancellationToken cancellationToken)
    {
        ValidateChannelAndIdentifier(channel, identifier);

        var link = await FindLinkAsync(channel, identifier, cancellationToken)
            ?? throw new NotFoundException("identity_not_found", "Identidade não encontrada para o canal e identificador informados.");

        return ToResponse(link.Customer, wasCreated: false, channel, identifier);
    }

    /// <summary>
    /// Busca (ou cria, se <paramref name="fullNameHint"/> for informado) o cliente pelo CPF,
    /// e garante o link de identidade para o canal/identificador atual. UC02 passos 4-5 e UC03.
    /// </summary>
    private async Task<ResolveIdentityResponse> ResolveByCpfAsync(
        string cpf, string linkChannel, string linkIdentifier, string? fullNameHint, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Cpf == cpf, cancellationToken);
        var wasCreated = false;

        if (customer is null)
        {
            if (string.IsNullOrWhiteSpace(fullNameHint))
            {
                throw new NotFoundException(
                    "cpf_not_found",
                    "CPF não cadastrado. Informe o nome completo (full_name_hint) para criar um novo cliente.",
                    new { field = "full_name_hint" });
            }

            customer = new Customer { Cpf = cpf, FullName = fullNameHint.Trim() };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(cancellationToken);
            wasCreated = true;

            _logger.LogInformation("New customer created via identity resolution on channel {Channel}", linkChannel);
        }

        _db.IdentityLinks.Add(new IdentityLink
        {
            CustomerId = customer.Id,
            Channel = linkChannel,
            Identifier = linkIdentifier,
        });
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ToResponse(customer, wasCreated, linkChannel, linkIdentifier);
    }

    private Task<IdentityLink?> FindLinkAsync(string channel, string identifier, CancellationToken cancellationToken)
        => _db.IdentityLinks
            .Include(l => l.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Channel == channel && l.Identifier == identifier, cancellationToken);

    private static void ValidateChannelAndIdentifier(string channel, string identifier)
    {
        if (!IdentifierFormat.IsSupportedChannel(channel))
            throw new ValidationException("invalid_channel", $"Canal '{channel}' não é suportado.");

        if (channel == Common.Contracts.Channels.Cpf && !IdentifierFormat.IsValidCpf(identifier))
            throw new ValidationException("invalid_cpf", "CPF inválido — verifique os dígitos digitados.");

        if (!IdentifierFormat.IsValidIdentifier(channel, identifier))
            throw new ValidationException("invalid_identifier", $"Identificador em formato inválido para o canal '{channel}'.");
    }

    private static ResolveIdentityResponse ToResponse(Customer customer, bool wasCreated, string channel, string identifier) => new(
        UnifiedCustomerId: customer.Id,
        Customer: new CustomerSummaryDto(customer.Id, customer.FullName, customer.Cpf),
        WasCreated: wasCreated,
        ResolvedLink: new ResolvedLinkDto(channel, identifier));
}
