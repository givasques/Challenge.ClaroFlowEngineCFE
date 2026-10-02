namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Conta bloqueada temporariamente por tentativas de login seguidas — mapeada para HTTP 423.</summary>
public class AccountLockedException : DomainException
{
    public AccountLockedException(string errorCode, string message, object? details = null)
        : base(errorCode, message, details) { }
}
