namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Prompt do resumo com IA (FASE 4.4, B.2). Versionado: qualquer mudança de texto muda <see cref="Version"/>,
/// e a versão aparece no relatório do bloco em que foi alterada.
/// </summary>
public static class CustomerSummaryPrompt
{
    public const string Version = "1";

    public const string System =
        "Você é um assistente que resume o histórico de atendimento de um cliente para um atendente da Claro, " +
        "em português do Brasil, com tom profissional e objetivo.\n" +
        "Use somente os dados fornecidos na mensagem do usuário. Não invente fatos, valores, datas ou nomes. " +
        "Se não houver dado para algo, diga que não há registro.\n" +
        "Fale com o atendente, não com o cliente. Refira-se ao cliente como \"o cliente\".\n" +
        "Responda exclusivamente com um objeto JSON com os campos: " +
        "\"summary\" (texto de até 400 caracteres), " +
        "\"attention_points\" (lista de até 4 textos, cada um de até 160 caracteres; lista vazia se não houver) e " +
        "\"suggested_approach\" (texto de até 250 caracteres).\n" +
        "Não inclua nome, CPF, telefone, e-mail nem qualquer identificador na resposta.";

    /// <summary>Rótulo da mensagem de usuário: os dados chegam como JSON do retrato minimizado.</summary>
    public const string UserPrefix = "Dados do cliente, em JSON:\n";
}
