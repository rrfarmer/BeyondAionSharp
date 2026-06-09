using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Questengine.Model;

/// <summary>Java parity: questEngine/model/ConditionOperation (@XmlEnum).</summary>
[XmlType("ConditionOperation")]
public enum ConditionOperation
{
    EQUAL,
    GREATER,
    GREATER_EQUAL,
    LESSER,
    LESSER_EQUAL,
    NOT_EQUAL,
    IN,
    NOT_IN
}

public static class ConditionOperationExtensions
{
    public static string Value(this ConditionOperation t) => t.ToString();

    public static ConditionOperation FromValue(string v) => (ConditionOperation) Enum.Parse(typeof(ConditionOperation), v);
}
