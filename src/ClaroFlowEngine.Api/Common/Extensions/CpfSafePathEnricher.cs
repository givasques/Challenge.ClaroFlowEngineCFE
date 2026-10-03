using Serilog.Core;
using Serilog.Events;

namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Enricher global do Serilog (FASE 4.3, item A.3): mascara qualquer CPF que apareça nas
/// propriedades de path de requisição (`RequestPath`, `Path`) de QUALQUER log — tanto o request
/// logging do Serilog.AspNetCore quanto o log nativo "Request finished" do ASP.NET Core (Hosting
/// Diagnostics), que sempre grava o path bruto independente de customização de MessageTemplate.
/// Roda sobre todo LogEvent antes dos sinks, então cobre qualquer logger que grave essas propriedades.
/// </summary>
public class CpfSafePathEnricher : ILogEventEnricher
{
    private static readonly string[] PathPropertyNames = ["RequestPath", "Path"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in PathPropertyNames)
        {
            if (logEvent.Properties.TryGetValue(name, out var value)
                && value is ScalarValue { Value: string path })
            {
                var masked = CpfMasking.MaskInPath(path);
                if (masked != path)
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, masked));
            }
        }
    }
}
