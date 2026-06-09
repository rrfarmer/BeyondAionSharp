using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ChallengeType.</summary>
[XmlType("ChallengeType")]
public enum ChallengeType
{
    LEGION,
    TOWN
}

public static class ChallengeTypeExtensions
{
    public static int GetId(this ChallengeType t) => t switch
    {
        ChallengeType.LEGION => 1,
        ChallengeType.TOWN => 2,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static string Value(this ChallengeType t) => t.ToString();

    public static ChallengeType FromValue(string paramString) => (ChallengeType) Enum.Parse(typeof(ChallengeType), paramString);
}
