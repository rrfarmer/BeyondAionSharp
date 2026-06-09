using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Questengine.Model;

/// <summary>Java parity: questEngine/model/ConditionUnionType (@XmlEnum).</summary>
[XmlType("ConditionUnionType")]
public enum ConditionUnionType
{
    AND,
    OR
}

public static class ConditionUnionTypeExtensions
{
    public static string Value(this ConditionUnionType t) => t.ToString();

    public static ConditionUnionType FromValue(string v) => (ConditionUnionType) Enum.Parse(typeof(ConditionUnionType), v);
}
