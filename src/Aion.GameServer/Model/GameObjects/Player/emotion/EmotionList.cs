using System.Collections.Generic;
using System.Linq;

namespace Aion.GameServer.Model.GameObjects.Player.Emotion;

/// <summary>Java parity: model/gameobjects/player/emotion/EmotionList.</summary>
public class EmotionList
{
    // Java parity: LinkedHashMap — insertion-ordered; Dictionary preserves insertion order absent removals.
    private Dictionary<int, Emotion> emotions;
    private Aion.GameServer.Model.GameObjects.Player.Player owner;

    public EmotionList(Aion.GameServer.Model.GameObjects.Player.Player owner)
    {
        this.owner = owner;
    }

    public void Add(int emotionId, int dispearTime, bool isNew)
    {
        if (emotions == null)
            emotions = new Dictionary<int, Emotion>();

        Emotion emotion = new Emotion(emotionId, dispearTime);
        emotions[emotionId] = emotion;

        if (isNew)
        {
            Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(emotion, owner);
            Aion.GameServer.Dao.PlayerEmotionListDAO.InsertEmotion(owner, emotion);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotionList((byte)1, new List<Emotion> { emotion }));
        }
    }

    public void Remove(int emotionId)
    {
        emotions.Remove(emotionId);
        Aion.GameServer.Dao.PlayerEmotionListDAO.DeleteEmotion(owner.GetObjectId(), emotionId);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotionList((byte)0, GetEmotions()));
    }

    public bool Contains(int emotionId)
    {
        return emotions != null && emotions.ContainsKey(emotionId);
    }

    public bool CanUse(int emotionId)
    {
        return !Aion.GameServer.Model.Templates.Item.Actions.EmotionLearnAction.IsLearnable(emotionId) || Contains(emotionId);
    }

    public ICollection<Emotion> GetEmotions()
    {
        if (emotions == null)
            return new List<Emotion>();
        return emotions.Values;
    }
}
