namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Recurso não encontrado — mapeada para HTTP 404.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
