using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Handlers.Zone.PvpZones;

/// <summary>Java parity: zone/pvpZones/PvPAreaZone (MrPoke) : PvPZone. @ZoneNameAnnotation("LC1_PVP_SUB_C_110010000 DC1_PVP_ZONE_120010000"); instanceof Player&&cond→is Player p&&cond; getEffectController()→(PlayerEffectController)GetEffectController() (Java covariant Player.getEffectController); ZoneName.get + == 1:1.</summary>
[ZoneNameAnnotation("LC1_PVP_SUB_C_110010000 DC1_PVP_ZONE_120010000")]
public class PvPAreaZone : PvPZone
{
    public override void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Player player && CustomConfig.KEEP_BUFFS_IN_COLISEUM)
            ((PlayerEffectController)player.GetEffectController()).SetKeepBuffsOnDie(true);
    }

    public override void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Player player)
            ((PlayerEffectController)player.GetEffectController()).SetKeepBuffsOnDie(false);
    }

    protected override void DoTeleport(Player player, ZoneName zoneName)
    {
        if (zoneName == ZoneName.Get("LC1_PVP_SUB_C_110010000"))
            TeleportService.TeleportTo(player, 110010000, 1, 1470.3f, 1343.5f, 563.7f);
        else if (zoneName == ZoneName.Get("DC1_PVP_ZONE_120010000"))
            TeleportService.TeleportTo(player, 120010000, 1, 1005.1f, 1528.9f, 222.1f);
    }
}
