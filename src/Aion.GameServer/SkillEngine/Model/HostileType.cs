namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Whether a skill's hostility is direct, indirect, or none.
/// Java parity: skillengine/model/HostileType (@XmlType("HostileType") @XmlEnum).
/// </summary>
public enum HostileType
{
    NONE,
    DIRECT,
    INDIRECT,
}
