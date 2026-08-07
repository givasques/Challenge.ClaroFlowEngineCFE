namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>
/// Estados válidos do ciclo de vida de uma jornada.
/// </summary>
public static class JourneyStatus
{
    public const string Open = "open";
    public const string Concluded = "concluded";
    public const string Expired = "expired";
    public const string Abandoned = "abandoned";
}
