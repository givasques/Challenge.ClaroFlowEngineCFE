using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Data.Configurations;

/// <summary>
/// Value comparer compartilhado para colunas JSONB mapeadas como Dictionary&lt;string, object&gt;.
/// Necessário porque o EF Core não sabe comparar dicionários mutáveis por padrão (change tracking).
/// </summary>
public static class JsonDictionaryValueComparer
{
    public static ValueComparer<Dictionary<string, object>> Instance { get; } = new(
        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
        d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
        d => JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
}
