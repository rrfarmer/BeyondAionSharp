namespace Aion.GameServer.Model;

/// <summary>
/// Creature gender.
/// Java parity: model/Gender (@XmlEnum).
/// </summary>
public enum Gender
{
    MALE = 0,
    FEMALE = 1,
}

public static class GenderExtensions
{
    // Java parity: getGenderId()
    public static int GetGenderId(this Gender gender) => (int)gender;
}
