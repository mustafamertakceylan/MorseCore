using System.Text.Json.Serialization;

namespace MorseCore.AppCore;

[JsonSourceGenerationOptions(WriteIndented=true)]
[JsonSerializable(typeof(Preferences))]
[JsonSerializable(typeof(Dictionary<string,string>))]
internal partial class AppJsonContext : JsonSerializerContext;
