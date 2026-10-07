using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using Xunit;

namespace ClaroFlowEngine.Api.Tests;

/// <summary>Validação da resposta do modelo (FASE 4.4, B.3), sem chamada a provedor.</summary>
public class SummaryResponseValidatorTests
{
    private const string Valid = "{\"summary\":\"Cliente com 1 atendimento.\",\"attention_points\":[\"Jornada parada há 3 dias.\"],\"suggested_approach\":\"Retomar a troca de plano.\"}";

    [Fact]
    public void Aceita_json_dentro_de_bloco_markdown()
    {
        var fenced = "```json\n" + Valid + "\n```";

        var result = SummaryResponseValidator.Parse(fenced);

        Assert.Equal("Cliente com 1 atendimento.", result.Summary);
        Assert.Single(result.AttentionPoints);
    }

    [Fact]
    public void Aceita_bloco_sem_identificador_de_linguagem()
    {
        var result = SummaryResponseValidator.Parse("```\n" + Valid + "\n```");

        Assert.Equal("Retomar a troca de plano.", result.SuggestedApproach);
    }

    [Theory]
    [InlineData("isto não é json", "JSON não interpretável")]
    [InlineData("{\"summary\":\"x\",\"suggested_approach\":\"y\"}", "campo attention_points ausente")]
    [InlineData("{\"summary\":\"x\",\"attention_points\":[],\"suggested_approach\":7}", "campo suggested_approach ausente ou não é texto")]
    [InlineData("{\"summary\":\"Cliente do CPF 123.456.789-09.\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}", "contém padrão de CPF")]
    [InlineData("{\"summary\":\"Ligar para (11) 98888-0001.\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}", "contém padrão de telefone")]
    [InlineData("{\"summary\":\"Escrever para ana@exemplo.com.br.\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}", "contém padrão de e-mail")]
    public void Falha_informa_o_motivo_sem_o_conteudo(string content, string expectedReason)
    {
        var error = Assert.Throws<ClaroFlowEngine.Api.Modules.CustomerInsights.Providers.AiProviderException>(
            () => SummaryResponseValidator.Parse(content));

        Assert.Equal("ai_invalid_response", error.Reason);
        Assert.Contains(expectedReason, error.Message);
        // A mensagem não repete o conteúdo da resposta.
        Assert.DoesNotContain("Cliente do CPF", error.Message);
        Assert.DoesNotContain("Ligar para", error.Message);
    }

    [Theory]
    [InlineData("Fatura de R$ 129,90 de 08/2026.")]
    [InlineData("Jornada parada há 3 dias.")]
    [InlineData("Valor R$ 1.299,90 em 08/2026 e 09/2026.")]
    [InlineData("Emitida em 06/10/2026 às 14:30.")]
    [InlineData("Parcela 3 de 12 de R$ 89,90.")]
    [InlineData("Total R$ 1.234.567,89 no mês de referência 10/2026.")]
    [InlineData("Dia de vencimento 12 e 2026-10 registrados.")]
    public void Valores_datas_e_meses_nao_sao_confundidos_com_telefone(string text)
    {
        var json = "{\"summary\":\"" + text + "\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}";

        var result = SummaryResponseValidator.Parse(json);

        Assert.Equal(text, result.Summary);
    }

    [Theory]
    [InlineData("(11) 98888-0001")]
    [InlineData("5511988880001")]
    [InlineData("11 98888-0001")]
    public void Telefones_reais_continuam_bloqueados(string phone)
    {
        var json = "{\"summary\":\"Contato " + phone + " registrado.\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}";

        Assert.Throws<ClaroFlowEngine.Api.Modules.CustomerInsights.Providers.AiProviderException>(
            () => SummaryResponseValidator.Parse(json));
    }
}
