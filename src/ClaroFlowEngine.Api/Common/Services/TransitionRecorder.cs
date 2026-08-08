using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Common.Services;

public class TransitionRecorder : ITransitionRecorder
{
    private readonly CfeDbContext _db;

    public TransitionRecorder(CfeDbContext db) => _db = db;

    public void Record(Guid journeyContextId, string channel, string eventType, string? description = null, object? metadata = null)
    {
        _db.JourneyTransitions.Add(new JourneyTransition
        {
            JourneyContextId = journeyContextId,
            Channel = channel,
            EventType = eventType,
            Description = description,
            Metadata = ToMetadataDictionary(metadata),
        });
    }

    // Aceita qualquer objeto anônimo/POCO e converte para o formato que a coluna JSONB espera.
    private static Dictionary<string, object> ToMetadataDictionary(object? metadata)
    {
        if (metadata is null) return new();
        if (metadata is Dictionary<string, object> dict) return dict;

        var json = JsonSerializer.Serialize(metadata);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
    }
}
