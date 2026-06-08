namespace Aion.GameServer.Model.GameObjects;

/// <summary>Mail letter type. Java parity: model/gameobjects/LetterType.</summary>
public enum LetterType
{
    NORMAL = 0,
    EXPRESS = 1,
    BLACKCLOUD = 2,
}

public static class LetterTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this LetterType type) => (int)type;

    // Java parity: static getLetterTypeById(int) — throws if unsupported.
    public static LetterType GetLetterTypeById(int id)
    {
        foreach (LetterType lt in Enum.GetValues<LetterType>())
        {
            if ((int)lt == id)
                return lt;
        }
        throw new ArgumentException("Unsupported revive type: " + id);
    }
}
