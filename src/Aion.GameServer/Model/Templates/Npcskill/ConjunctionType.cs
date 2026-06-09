using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/ConjunctionType (nrg).</summary>
[XmlType("ConjunctionType")]
public enum ConjunctionType
{
    AND,
    OR,
    XOR,
}

public static class ConjunctionTypeExtensions
{
    // Java parity: value()
    public static string Value(this ConjunctionType type) => type.ToString();

    // Java parity: fromValue(String)
    public static ConjunctionType FromValue(string v) => (ConjunctionType)System.Enum.Parse(typeof(ConjunctionType), v);
}
