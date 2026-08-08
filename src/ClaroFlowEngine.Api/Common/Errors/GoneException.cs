namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Recurso expirou ou foi invalidado (token usado/expirado, jornada expirada) — mapeada para HTTP 410.</summary>
public class GoneException : DomainException
{
    public GoneException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
