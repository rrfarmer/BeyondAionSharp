using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ToyPetSpawnAction.</summary>
[XmlType("ToyPetSpawnAction")]
public class ToyPetSpawnAction : AbstractItemAction
{
    [XmlAttribute("npcid")] public int npcid;
    [XmlAttribute("time")] public int time;

    public int GetNpcId()
    {
        return npcid;
    }

    public int GetTime()
    {
        return time;
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (player.IsFlying())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_BINDSTONE_ITEM_WHILE_FLYING());
            return false;
        }
        if (player.IsInInstance())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_REGISTER_BINDSTONE_FAR_FROM_NPC());
            return false;
        }
        if (Aion.GameServer.Services.KiskService.GetInstance().HaveKisk(player.GetObjectId()) && Aion.GameServer.Configs.Main.CustomConfig.ENABLE_KISK_RESTRICTION)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_BINDSTONE_ALREADY_INSTALLED());
            return false;
        }
        if (!IsPutKiskZone(player))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_INVALID_LOCATION());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // ShowAction
        player.GetController().CancelUseItem();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 10000, 0, 0), true);
        ItemUseObserver observer = new ToyPetUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 1), true);
            player.GetObserveController().RemoveObserver(observer);
            // RemoveKisk
            if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
                return ValueTask.CompletedTask;
            float x = player.GetX();
            float y = player.GetY();
            float z = player.GetZ();
            byte heading = (byte)((player.GetHeading() + 60) % 120);
            int worldId = player.GetWorldId();
            int instanceId = player.GetInstanceId();
            Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(worldId, npcid, x, y, z, heading);

            Kisk kisk = Aion.GameServer.SpawnEngine.VisibleObjectSpawner.SpawnKisk(spawn, instanceId, player);
            int objOwnerId = player.GetObjectId();
            // Schedule Despawn Action
            Aion.GameServer.Utils.ScheduledTask task = Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct2 =>
            {
                kisk.GetController().Delete();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromSeconds(kisk.GetRemainingLifetime()));
            kisk.GetController().AddTask(Aion.GameServer.Model.TaskId.DESPAWN, task);

            // ShowFinalAction
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Services.KiskService.GetInstance().RegKisk(kisk, objOwnerId);

            if (kisk.GetMaxMembers() > 1)
                kisk.GetController().OnDialogRequest(player);
            else
                Aion.GameServer.Services.KiskService.GetInstance().OnBind(kisk, player);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000)));
    }

    private bool IsPutKiskZone(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        foreach (Aion.GameServer.World.Zone.ZoneInstance zone in player.FindZones())
        {
            if (!zone.CanPutKisk())
                return false;
        }
        return true;
    }

    // Java parity: anonymous ItemUseObserver in act() (note: Java's abort does not removeObserver).
    private sealed class ToyPetUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public ToyPetUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
        }
    }
}
