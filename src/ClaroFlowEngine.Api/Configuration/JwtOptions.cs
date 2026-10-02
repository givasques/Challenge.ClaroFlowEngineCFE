namespace ClaroFlowEngine.Api.Configuration;

/// <summary>
/// Configuração do JWT usado pelo painel do atendente (FASE 4.1). <see cref="SigningKey"/> nunca é
/// versionado — vem de variável de ambiente, user-secrets ou appsettings.Development.json (gitignored).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "cfe-api";
    public string Audience { get; set; } = "cfe-attendant-panel";
    public string SigningKey { get; set; } = "";
    public int ExpirationHours { get; set; } = 8;
}
