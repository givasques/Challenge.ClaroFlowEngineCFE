namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Estado atual do recurso é incompatível com a operação — mapeada para HTTP 409.</summary>
public class ConflictException : DomainException
{
    public ConflictException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
