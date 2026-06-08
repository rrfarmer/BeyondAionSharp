namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Which hit a proc/modifier applies to.
/// Java parity: skillengine/model/HitType (@XmlType("HitType") @XmlEnum).
/// </summary>
public enum HitType
{
    EVERYHIT,
    NMLATK,
    MAHIT,
    PHHIT,
    FEAR,
    SKILL,
    BACKATK,
}
