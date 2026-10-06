namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Resumo do cliente gerado por IA (FASE 4.4). Guarda só o resultado gerado por IA, com a impressão digital
/// dos dados que o originaram. Resumos gerados por regras não são guardados.
/// </summary>
public class CustomerAiSummary
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>SHA-256 dos fatos que compõem o retrato do cliente. Muda quando os dados mudam, não com a passagem do tempo.</summary>
    public string InputFingerprint { get; set; } = string.Empty;

    /// <summary>"ai" ou "rules". Só "ai" é guardado.</summary>
    public string Source { get; set; } = string.Empty;

    public string? Model { get; set; }

    public Dictionary<string, object> Content { get; set; } = new();

    public DateTime GeneratedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}
