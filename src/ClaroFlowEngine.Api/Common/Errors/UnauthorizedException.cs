namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Credenciais inválidas ou sessão ausente/expirada — mapeada para HTTP 401.</summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
