using Aion.GameServer.Model;

namespace Aion.GameServer.Model.GameObjects.Players.Emotion;

/// <summary>Java parity: model/gameobjects/player/emotion/Emotion implements Expirable.</summary>
public class Emotion : IExpirable
{
    private int id;
    private int expireTime;

    public Emotion(int id, int expireTime)
    {
        this.id = id;
        this.expireTime = expireTime;
    }

    /// <summary>the id</summary>
    public int GetId()
    {
        return id;
    }

    public int GetExpireTime()
    {
        return expireTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.GetEmotions().Remove(id);
        // TODO emotion templates -> parse nameIds for system message, like 600228 for STR_EMOTION_CASH_DISCODANCE (Aion Boogie) etc.
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_SOCIALACTION_BY_TIMEOUT(/* nameId */));
    }
}
