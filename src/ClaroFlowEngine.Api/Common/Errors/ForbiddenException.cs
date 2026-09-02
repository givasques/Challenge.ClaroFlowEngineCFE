namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Canal autenticado não tem permissão para esta operação — mapeada para HTTP 403.</summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
