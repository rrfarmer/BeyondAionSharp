namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Sub-type of a skill (attack, heal, buff, summon, ...).
/// Java parity: skillengine/model/SkillSubType (@XmlType("skillSubType") @XmlEnum).
/// </summary>
public enum SkillSubType
{
    NONE,
    ATTACK,
    CHANT,
    HEAL,
    BUFF,
    DEBUFF,
    SUMMON,
    SUMMONHOMING,
    SUMMONTRAP,
}
