using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/AnimationAddAction.</summary>
[XmlType("AnimationAddAction")]
public class AnimationAddAction : AbstractItemAction
{
    [XmlAttribute("idle")] protected int? idle;
    [XmlAttribute("run")] protected int? run;
    [XmlAttribute("jump")] protected int? jump;
    [XmlAttribute("rest")] protected int? rest;
    [XmlAttribute("shop")] protected int? shop;
    [XmlAttribute("minutes")] protected int? minutes;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (parentItem == null) // no item selected.
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_COLOR_ERROR());
            return false;
        }

        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        player.GetController().CancelUseItem();
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 1000, 0, 0));
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (player.GetInventory().DecreaseItemCount(parentItem, 1) != 0)
                return ValueTask.CompletedTask;
            if (idle != null)
                AddMotion(player, idle.Value);
            if (run != null)
                AddMotion(player, run.Value);
            if (jump != null)
                AddMotion(player, jump.Value);
            if (rest != null)
                AddMotion(player, rest.Value);
            if (shop != null)
                AddMotion(player, shop.Value);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmMotion(player.GetObjectId(), player.GetMotions().GetActiveMotions()), false);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000)));
    }

    private void AddMotion(Aion.GameServer.Model.GameObjects.Players.Player player, int motionId)
    {
        Aion.GameServer.Model.GameObjects.Players.Motion.Motion motion = new Aion.GameServer.Model.GameObjects.Players.Motion.Motion(motionId, minutes == null ? 0 : (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + minutes.Value * 60, true);
        player.GetMotions().Add(motion, true);
        // Java parity: default interface method — C# requires an explicit IExpirable cast (foundational diff).
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmMotion((short)motion.GetId(), ((Aion.GameServer.Model.IExpirable)motion).SecondsUntilExpiration()));
    }
}
