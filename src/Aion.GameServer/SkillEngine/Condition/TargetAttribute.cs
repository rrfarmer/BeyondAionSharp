namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Target classification for a skill condition.
/// Java parity: skillengine/condition/TargetAttribute (@XmlType("TargetAttribute") @XmlEnum).
/// </summary>
public enum TargetAttribute
{
    NPC,
    PC,
    ALL,
    SELF,
    NONE,
}

public static class TargetAttributeExtensions
{
    // Java parity: value() — returns the constant name.
    public static string Value(this TargetAttribute attribute) => attribute.ToString();

    // Java parity: static fromValue(String) — valueOf.
    public static TargetAttribute FromValue(string v) => Enum.Parse<TargetAttribute>(v);
}
