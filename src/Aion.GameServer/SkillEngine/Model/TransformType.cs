namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Kind of creature transformation.
/// Java parity: skillengine/model/TransformType (@XmlType("TransformType") @XmlEnum).
/// </summary>
public enum TransformType
{
    NONE = 0,
    PC = 1,
    AVATAR = 2,
    FORM1 = 3,
}

public static class TransformTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this TransformType type) => (int)type;
}
