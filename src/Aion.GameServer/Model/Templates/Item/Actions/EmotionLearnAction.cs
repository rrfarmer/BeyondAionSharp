using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/EmotionLearnAction.</summary>
[XmlType("EmotionLearnAction")]
public class EmotionLearnAction : AbstractItemAction
{
    // Java parity: ConcurrentHashMap.newKeySet() — concurrent int set (BCL has no ConcurrentHashSet, use a ConcurrentDictionary as a set).
    private static readonly ConcurrentDictionary<int, byte> LEARNABLE_IDS = new ConcurrentDictionary<int, byte>();

    [XmlAttribute("emotionid")] private int emotionId;
    [XmlAttribute("minutes")] private int minutes;

    // Java parity: afterUnmarshal(Unmarshaller, Object) — invoked by the loader after deserialization.
    public void AfterUnmarshal()
    {
        LEARNABLE_IDS[emotionId] = 0;
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (emotionId == 0 || parentItem == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_COLOR_ERROR());
            return false;
        }
        if (player.GetEmotions() != null && player.GetEmotions().Contains(emotionId))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_TOOLTIP_LEARNED_EMOTION());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Item.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

        player.GetEmotions().Add(emotionId, minutes == 0 ? 0 : (int)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + minutes * 60, true);
        player.GetInventory().Delete(parentItem);
    }

    /// <summary>True if there exists a learn template for given emotion.</summary>
    public static bool IsLearnable(int emotionId)
    {
        return LEARNABLE_IDS.ContainsKey(emotionId);
    }

    public static List<int> GetLearnableEmotionIds()
    {
        return LEARNABLE_IDS.Keys.OrderBy(x => x).ToList();
    }

    public int GetEmotionId()
    {
        return emotionId;
    }
}
