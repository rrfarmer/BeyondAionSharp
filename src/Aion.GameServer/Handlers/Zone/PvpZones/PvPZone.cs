using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Handlers.Zone.PvpZones;

/// <summary>Java parity: zone/pvpZones/PvPZone (MrPoke) implements AdvancedZoneHandler. instanceof→is pattern; zone.forEach(lambda)→ForEach; equals→Equals; ThreadPoolManager.schedule(Runnable,5000)→Schedule(Action,5000); getController().addTask/getAndRemoveTask 1:1.</summary>
public abstract class PvPZone : AdvancedZoneHandler
{
    public virtual void OnEnterZone(Creature player, ZoneInstance zone)
    {
    }

    public virtual void OnLeaveZone(Creature player, ZoneInstance zone)
    {
    }

    public bool OnDie(Creature lastAttacker, Creature target, ZoneInstance zone)
    {
        if (!(target is Player player))
            return false;

        if (zone is PvPZoneInstance)
        {
            zone.ForEach(creature =>
            {
                if (creature is Player p)
                {
                    if (p.Equals(player))
                        PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_PvPZONE_MY_DEATH_TO_B(lastAttacker.GetName()));
                    else if (p.Equals(lastAttacker))
                        PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_PvPZONE_HOSTILE_DEATH_TO_ME(player.GetName()));
                    else
                        PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_PvPZONE_HOSTILE_DEATH_TO_B(lastAttacker.GetName(), player.GetName()));
                }
            });

            player.GetController().AddTask(TaskId.TELEPORT, ThreadPoolManager.GetInstance().Schedule(() =>
            {
                player.GetController().GetAndRemoveTask(TaskId.TELEPORT); // remove manually as it won't get removed automatically
                PlayerReviveService.DuelRevive(player);
                DoTeleport(player, zone.GetZoneTemplate().GetName());
                PacketSendUtility.BroadcastToZone(zone, SM_SYSTEM_MESSAGE.STR_PvPZONE_OUT_MESSAGE(player.GetName()));
            }, 5000));
        }
        return true;
    }

    protected abstract void DoTeleport(Player player, ZoneName zoneName);
}
