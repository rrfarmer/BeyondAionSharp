using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/RewardType.</summary>
[XmlType("Rates")]
public enum RewardType
{
    NONE,
    POINT,
    SPAWN
}

/// <summary>Java parity: RewardType.value()/fromValue(String).</summary>
public static class RewardTypeExtensions
{
    public static string Value(this RewardType type)
    {
        return type.ToString();
    }

    public static RewardType FromValue(string paramString)
    {
        return (RewardType) Enum.Parse(typeof(RewardType), paramString);
    }
}
