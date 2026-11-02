using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players.Motion;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Utils;

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

    public IDictionary<MotionType, int> GetMotionIds()
    {
        var ids = new SortedDictionary<MotionType, int>();
        if (idle != null) ids[MotionType.IDLE] = idle.Value;
        if (run != null) ids[MotionType.RUN] = run.Value;
        if (jump != null) ids[MotionType.JUMP] = jump.Value;
        if (rest != null) ids[MotionType.REST] = rest.Value;
        if (shop != null) ids[MotionType.SHOP] = shop.Value;
        return ids;
    }

    private bool HasAnyMotion(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        foreach (int id in GetMotionIds().Values)
            if (player.GetMotions().GetMotions().ContainsKey(id))
                return true;
        return false;
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (parentItem == null) // no item selected.
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_ERROR());
            return false;
        }
        if (HasAnyMotion(player))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_CUSTOMANIMATION_ALREADY_HAS_MOTION());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int castingDelay = player.GetItemUseDelay(parentItem.GetItemTemplate().GetCastingDelay());
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem);
            return;
        }
        var observer = new AnimationAddObserver(player, parentItem);

        player.GetObserveController().AddObserver(observer);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), castingDelay, ItemUseAnimation.USE_START));
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    // Java parity: anonymous ItemUseObserver in act.
    private sealed class AnimationAddObserver : Aion.GameServer.Controllers.Observer.ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public AnimationAddObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
            : base(player)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        protected override void OnAbort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, ItemUseAnimation.USE_CANCEL));
        }
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
    {
        if (HasAnyMotion(player))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, ItemUseAnimation.USE_FAIL));
            return;
        }
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;
        player.StartCooldown(parentItem);
        foreach (int motionId in GetMotionIds().Values)
            AddMotion(player, motionId);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, ItemUseAnimation.USE_SUCCESS));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION.PlayerMotions(player), false);
    }

    private void AddMotion(Aion.GameServer.Model.GameObjects.Players.Player player, int motionId)
    {
        Aion.GameServer.Model.GameObjects.Players.Motion.Motion motion = new Aion.GameServer.Model.GameObjects.Players.Motion.Motion(motionId, minutes == null ? 0 : (int)SystemClock.CurrentSeconds() + minutes.Value * 60, true);
        player.GetMotions().Add(motion, true);
        // Java parity: default interface method — C# requires an explicit IExpirable cast (foundational diff).
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION.Add(motion));
    }
}
