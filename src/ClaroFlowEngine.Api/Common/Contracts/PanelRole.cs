namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Perfis de usuário do painel do atendente (FASE 4.1).</summary>
public static class PanelRole
{
    public const string Attendant = "attendant";
    public const string Manager = "manager";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [Attendant] = "Atendente",
        [Manager] = "Gestor",
    };

    public static bool IsValid(string role) => Labels.ContainsKey(role);

    public static string Label(string role) => Labels.GetValueOrDefault(role, role);
}
