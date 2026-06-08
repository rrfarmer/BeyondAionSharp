namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>
/// Species filter (PC vs NPC) for a skill's target.
/// Java parity: skillengine/properties/TargetSpeciesAttribute (@XmlType("TargetSpeciesAttribute") @XmlEnum).
/// </summary>
public enum TargetSpeciesAttribute
{
    PC,
    NPC,
}
