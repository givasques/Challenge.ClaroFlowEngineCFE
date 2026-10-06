using System.Threading.RateLimiting;
using ClaroFlowEngine.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Services;

public interface IAiSummaryRateLimiter
{
    /// <summary>Consome uma geração para o atendente. Retorna false se ele já atingiu o limite da janela.</summary>
    bool TryAcquire(Guid panelUserId);
}

/// <summary>
/// Limite de gerações por atendente (FASE 4.4, A.6). Mesmo tipo de janela fixa da policy de login (P40 do guia),
/// mas chamado pelo serviço depois da checagem de cache, para que respostas do cache não consumam o limite.
/// </summary>
public sealed class AiSummaryRateLimiter : IAiSummaryRateLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public AiSummaryRateLimiter(IOptions<AiSummaryOptions> options)
    {
        var permitLimit = options.Value.RequestsPerUserPerMinute;
        _limiter = PartitionedRateLimiter.Create<string, string>(
            key => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    public bool TryAcquire(Guid panelUserId)
    {
        using var lease = _limiter.AttemptAcquire(panelUserId.ToString());
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
