namespace Aion.GameServer.SkillEngine.Change;

/// <summary>
/// How a stat-change value is applied (additive, percentage, or replacement).
/// Java parity: skillengine/change/Func (@XmlType("Func") @XmlEnum).
/// </summary>
public enum Func
{
    ADD,
    PERCENT,
    REPLACE,
}
