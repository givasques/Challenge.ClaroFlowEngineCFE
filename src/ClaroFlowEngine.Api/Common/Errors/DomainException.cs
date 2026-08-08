namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>
/// Base para exceções de negócio conhecidas, que carregam um código de erro
/// e são convertidas em respostas HTTP padronizadas pelo middleware global.
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public object? Details { get; }

    protected DomainException(string errorCode, string message, object? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
