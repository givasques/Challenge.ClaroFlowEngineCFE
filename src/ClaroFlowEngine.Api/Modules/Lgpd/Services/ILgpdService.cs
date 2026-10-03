using ClaroFlowEngine.Api.Modules.Lgpd.Dtos;

namespace ClaroFlowEngine.Api.Modules.Lgpd.Services;

public interface ILgpdService
{
    Task<RightToBeForgottenResponse> ExerciseRightToBeForgottenAsync(string cpf, CancellationToken cancellationToken);

    Task<CpfRevealResponse> RevealCpfAsync(Guid customerId, string reason, CancellationToken cancellationToken);
}
