using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players.Emotion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EMOTION_LIST. Sends the player's emotion list (id + seconds until expiration) for a given action. Converges PlayerEnterWorldService. Collection->ICollection. Emotion/AionServerPacket red-tolerated.</summary>
public class SM_EMOTION_LIST : AionServerPacket
{
    private readonly byte action;
    private readonly ICollection<Emotion> emotions;

    public SM_EMOTION_LIST(byte action, ICollection<Emotion> emotions)
    {
        this.action = action;
        this.emotions = emotions;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        WriteH(emotions.Count);
        foreach (Emotion emotion in emotions)
        {
            WriteD(emotion.GetId());
            WriteH(emotion.SecondsUntilExpiration());
        }
    }
}
