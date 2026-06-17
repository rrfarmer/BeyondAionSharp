using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/AnimationAddAction.</summary>
[XmlType("AnimationAddAction")]
public class AnimationAddAction : AbstractItemAction
{
    // Java parity: nullable Integer @XmlAttribute fields. XmlSerializer cannot bind Nullable<T> as an attribute,
    // so each round-trips via a string proxy (null when the attribute is absent).
    [XmlIgnore] public int? idle;
    [XmlIgnore] public int? run;
    [XmlIgnore] public int? jump;
    [XmlIgnore] public int? rest;
    [XmlIgnore] public int? shop;
    [XmlIgnore] public int? minutes;

    [XmlAttribute("idle")] public string IdleRaw { get => idle?.ToString(); set => idle = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("run")] public string RunRaw { get => run?.ToString(); set => run = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("jump")] public string JumpRaw { get => jump?.ToString(); set => jump = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("rest")] public string RestRaw { get => rest?.ToString(); set => rest = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("shop")] public string ShopRaw { get => shop?.ToString(); set => shop = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("minutes")] public string MinutesRaw { get => minutes?.ToString(); set => minutes = value == null ? (int?)null : int.Parse(value); }

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
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 1000, 0, 0));
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
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION(player.GetObjectId(), player.GetMotions().GetActiveMotions()), false);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000)));
    }

    private void AddMotion(Aion.GameServer.Model.GameObjects.Players.Player player, int motionId)
    {
        Aion.GameServer.Model.GameObjects.Players.Motion.Motion motion = new Aion.GameServer.Model.GameObjects.Players.Motion.Motion(motionId, minutes == null ? 0 : (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + minutes.Value * 60, true);
        player.GetMotions().Add(motion, true);
        // Java parity: default interface method — C# requires an explicit IExpirable cast (foundational diff).
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION((short)motion.GetId(), ((Aion.GameServer.Model.IExpirable)motion).SecondsUntilExpiration()));
    }
}
