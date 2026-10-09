using ClaroFlowEngine.Api.Modules.Auth.Dtos;

namespace ClaroFlowEngine.Api.Modules.Auth.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<PanelUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
