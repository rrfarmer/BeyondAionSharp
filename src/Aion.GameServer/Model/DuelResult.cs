namespace Aion.GameServer.Model;

/// <summary>Duel outcome (client message id + result id). Java parity: model/DuelResult.</summary>
public enum DuelResult
{
    DUEL_WON,
    DUEL_LOST,
    DUEL_DRAW,
}

public static class DuelResultExtensions
{
    // Java parity: per-constant (msgId, resultId).
    private static readonly Dictionary<DuelResult, (int MsgId, byte ResultId)> Data = new()
    {
        [DuelResult.DUEL_WON] = (1300098, 2),
        [DuelResult.DUEL_LOST] = (1300099, 0),
        [DuelResult.DUEL_DRAW] = (1300100, 1),
    };

    // Java parity: getMsgId()
    public static int GetMsgId(this DuelResult result) => Data[result].MsgId;

    // Java parity: getResultId()
    public static byte GetResultId(this DuelResult result) => Data[result].ResultId;
}
