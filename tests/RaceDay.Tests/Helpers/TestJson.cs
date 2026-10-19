using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Tests.Helpers;

/// <summary>JSON settings matching the API (camelCase, enums as strings) for reading responses.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
