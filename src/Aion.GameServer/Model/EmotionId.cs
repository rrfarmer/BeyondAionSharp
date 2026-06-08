namespace Aion.GameServer.Model;

/// <summary>
/// Emotion animation ids (some are NPC-only, e.g. ANGRY/THANK/THINK/SURPRISE).
/// Java parity: model/EmotionId (enum value = client id).
/// </summary>
public enum EmotionId
{
    NONE = 0,
    LAUGH = 1,
    ANGRY = 2,
    SAD = 3,
    POINT = 5,
    YES = 6,
    NO = 7,
    VICTORY = 8,
    CLAP = 11,
    SIGH = 12,
    SURPRISE = 13,
    COMFORT = 14,
    THANK = 15,
    BEG = 16,
    BLUSH = 17,
    SMILE = 28,
    SALUTE = 29,
    PANIC = 30,
    SORRY = 31,
    THINK = 33,
    DISLIKE = 34,
    STAND = 128, // All action NPCs having animation quest_actstanding
}

public static class EmotionIdExtensions
{
    // Java parity: id()
    public static int Id(this EmotionId emotion) => (int)emotion;
}
