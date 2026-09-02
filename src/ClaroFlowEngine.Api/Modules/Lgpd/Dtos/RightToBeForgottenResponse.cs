namespace ClaroFlowEngine.Api.Modules.Lgpd.Dtos;

/// <summary>Resposta de POST /customers/{cpf}/right-to-be-forgotten.</summary>
public record RightToBeForgottenResponse(
    string Status,
    Guid CustomerId,
    DateTime AnonymizedAt,
    AnonymizationOperationsDto Operations);

public record AnonymizationOperationsDto(
    string CustomerRecord,
    string IdentityLinks,
    string JourneyPayloads,
    int JourneysPreserved,
    int TransitionsPreserved);
