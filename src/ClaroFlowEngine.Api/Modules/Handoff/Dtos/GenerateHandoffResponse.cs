namespace ClaroFlowEngine.Api.Modules.Handoff.Dtos;

public record GenerateHandoffResponse(string Token, string TargetChannel, string DeepLinkUrl, DateTime ExpiresAt);
