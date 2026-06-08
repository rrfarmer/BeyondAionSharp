using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/SpawnType.</summary>
[XmlType("SpawnType")]
public enum SpawnType
{
    Manager,
    Teleport,
    Sign,
}

public static class SpawnTypeExtensions
{
    // Java parity: value() → name()
    public static string Value(this SpawnType t) => t.ToString().ToUpperInvariant();
    // Java parity: fromValue(String)
    public static SpawnType FromValue(string v) => Enum.Parse<SpawnType>(v, ignoreCase: true);
}
