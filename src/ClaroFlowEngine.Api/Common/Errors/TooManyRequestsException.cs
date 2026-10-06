namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Limite de uso excedido por um usuário (HTTP 429). Usado pelo resumo com IA (FASE 4.4).</summary>
public class TooManyRequestsException : DomainException
{
    public TooManyRequestsException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details)
    {
    }
}
