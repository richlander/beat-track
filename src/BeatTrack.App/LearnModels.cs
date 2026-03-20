using System.Text.Json.Serialization;

namespace BeatTrack.App;

public class MbLearnArtist
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("begin-area")] public MbLearnArea? BeginArea { get; set; }
    [JsonPropertyName("area")] public MbLearnArea? Area { get; set; }
    [JsonPropertyName("life-span")] public MbLearnLifeSpan? LifeSpan { get; set; }
    [JsonPropertyName("tags")] public List<MbLearnTag>? Tags { get; set; }
    [JsonPropertyName("relations")] public List<MbLearnRelation>? Relations { get; set; }
}

public class MbLearnArea
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class MbLearnLifeSpan
{
    [JsonPropertyName("begin")] public string? Begin { get; set; }
    [JsonPropertyName("end")] public string? End { get; set; }
    [JsonPropertyName("ended")] public bool Ended { get; set; }
}

public class MbLearnTag
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}

public class MbLearnRelation
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "";
    [JsonPropertyName("artist")] public MbLearnRelatedArtist? Artist { get; set; }
    [JsonPropertyName("attributes")] public List<string>? Attributes { get; set; }
}

public class MbLearnRelatedArtist
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

[JsonSerializable(typeof(MbLearnArtist))]
internal sealed partial class LearnJsonContext : JsonSerializerContext;
