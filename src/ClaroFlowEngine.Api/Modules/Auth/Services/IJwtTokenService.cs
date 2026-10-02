using ClaroFlowEngine.Api.Data.Entities;

namespace ClaroFlowEngine.Api.Modules.Auth.Services;

public interface IJwtTokenService
{
    (string AccessToken, DateTime ExpiresAt) GenerateToken(PanelUser user);
}
