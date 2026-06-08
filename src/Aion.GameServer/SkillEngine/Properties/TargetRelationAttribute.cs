namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>
/// Required relationship between caster and target.
/// Java parity: skillengine/properties/TargetRelationAttribute (@XmlType("TargetRelationAttribute") @XmlEnum).
/// </summary>
public enum TargetRelationAttribute
{
    NONE,
    ENEMY,
    MYPARTY,
    ALL,
    FRIEND,
}
