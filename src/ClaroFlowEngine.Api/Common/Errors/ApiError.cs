namespace ClaroFlowEngine.Api.Common.Errors;

/// <summary>Formato padronizado de resposta de erro da API.</summary>
public record ApiError(string ErrorCode, string Message, object? Details = null);
