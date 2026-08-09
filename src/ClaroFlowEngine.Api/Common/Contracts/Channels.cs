namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>
/// Canais suportados para identificadores e origem/destino de jornada.
/// </summary>
public static class Channels
{
    public const string Whatsapp = "whatsapp";
    public const string App = "app";
    public const string Cpf = "cpf";
    public const string Call = "call";
    public const string System = "system";

    /// <summary>Painel do atendente — chamador válido via X-Channel-Token, mas não origina/recebe jornadas.</summary>
    public const string Panel = "panel";

    /// <summary>Canais válidos para origem/encerramento de jornada (exclui "cpf", que é só identificador, e "system").</summary>
    public static readonly string[] JourneyChannels = [Whatsapp, App, Call];
}
