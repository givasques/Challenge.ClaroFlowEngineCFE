namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Payload ou parâmetro em formato inválido — mapeada para HTTP 400.</summary>
public class ValidationException : DomainException
{
    public ValidationException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
