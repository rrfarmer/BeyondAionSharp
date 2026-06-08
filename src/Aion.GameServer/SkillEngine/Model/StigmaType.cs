namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Stigma tier of a skill.
/// Java parity: skillengine/model/StigmaType (@XmlType("StigmaType") @XmlEnum).
/// </summary>
public enum StigmaType
{
    NONE = 0,
    BASIC = 1,
    ADVANCED = 2,
}

public static class StigmaTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this StigmaType type) => (int)type;
}
