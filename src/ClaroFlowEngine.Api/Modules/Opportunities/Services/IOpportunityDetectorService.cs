namespace ClaroFlowEngine.Api.Modules.Opportunities.Services;

public interface IOpportunityDetectorService
{
    /// <summary>Executa as 4 regras de detecção e persiste as oportunidades novas. Retorna a contagem criada por categoria.</summary>
    Task<Dictionary<string, int>> DetectAllAsync(CancellationToken cancellationToken);
}
