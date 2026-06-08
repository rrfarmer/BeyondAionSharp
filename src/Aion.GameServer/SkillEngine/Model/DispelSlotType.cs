namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Slot category targeted by a dispel.
/// Java parity: skillengine/model/DispelSlotType (@XmlType("DispelSlotType") @XmlEnum).
/// </summary>
public enum DispelSlotType
{
    BUFF,
    DEBUFF,
    SPECIAL2,
}
