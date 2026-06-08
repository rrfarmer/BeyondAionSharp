namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Scaling basis for an effect "hop" (per-damage vs per-skill-level).
/// Java parity: skillengine/model/HopType (@XmlType("HopType") @XmlEnum).
/// </summary>
public enum HopType
{
    DAMAGE,
    SKILLLV,
}
