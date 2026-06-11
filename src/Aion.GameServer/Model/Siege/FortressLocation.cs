using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/FortressLocation.</summary>
public class FortressLocation : SiegeLocation
{
    private readonly ConcurrentDictionary<int, ShieldObserver> shieldObservers = new ConcurrentDictionary<int, ShieldObserver>();

    public FortressLocation(SiegeLocationTemplate template)
        : base(template)
    {
    }

    public List<SiegeLegionReward> GetLegionRewards()
    {
        return GetTemplate().GetSiegeLegionRewards();
    }

    public List<SiegeMercenaryZone> GetSiegeMercenaryZones()
    {
        return GetTemplate().GetSiegeMercenaryZones();
    }

    public bool IsEnemy(Creature creature)
    {
        return creature.GetRace().GetRaceId() != GetRace().GetRaceId();
    }

    public override void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        base.OnEnterZone(creature, zone);
        creature.SetInsideZoneType(ZoneType.SIEGE);
        CheckForBalanceBuff(creature, SiegeBuffAction.ADD);
        if (IsUnderShield() && GetRace() != SiegeRaceExtensions.GetByRace(creature.GetRace()))
        {
            ShieldObserver observer = ShieldService.GetInstance().CreateShieldObserver(this, creature);
            if (observer != null)
            {
                creature.GetObserveController().AddObserver(observer);
                shieldObservers[creature.GetObjectId()] = observer;
            }
        }
    }

    public override void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        base.OnLeaveZone(creature, zone);
        creature.UnsetInsideZoneType(ZoneType.SIEGE);
        CheckForBalanceBuff(creature, SiegeBuffAction.LEAVE_ZONE_REMOVE);
        if (shieldObservers.TryRemove(creature.GetObjectId(), out ShieldObserver observer) && observer != null)
            creature.GetObserveController().RemoveObserver(observer);
    }

    public void CheckForBalanceBuff(Creature creature, SiegeBuffAction siegeBuffAction)
    {
        if (creature is Player && IsVulnerable() && GetFactionBalance() != 0)
        {
            switch (siegeBuffAction)
            {
                case SiegeBuffAction.LEAVE_ZONE_REMOVE:
                case SiegeBuffAction.SIEGE_END_REMOVE:
                    for (int i = 8867; i <= 8884; i++)
                    {
                        if (creature.GetEffectController().HasAbnormalEffect(i))
                        {
                            creature.GetEffectController().RemoveEffect(i);
                            if (creature.GetRace() == Race.ELYOS)
                            {
                                PacketSendUtility.SendPacket((Player) creature, siegeBuffAction == SiegeBuffAction.LEAVE_ZONE_REMOVE ?
                                        SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_LIGHT_GET_OUT_AREA() : SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_LIGHT_MIST_OFF());
                            }
                            else
                            {
                                PacketSendUtility.SendPacket((Player) creature, siegeBuffAction == SiegeBuffAction.LEAVE_ZONE_REMOVE ?
                                        SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_DARK_GET_OUT_AREA() : SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_DARK_MIST_OFF());
                            }
                            break;
                        }
                    }
                    break;
                case SiegeBuffAction.ADD:
                    int balance = GetFactionBalance();
                    if (creature.GetRace() == Race.ELYOS)
                    {
                        if (balance < 0)
                        {
                            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(8866 + Math.Abs(balance), creature, creature);
                            PacketSendUtility.SendPacket((Player) creature, SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_LIGHT_GAIN());
                        }
                        else
                        {
                            PacketSendUtility.SendPacket((Player) creature, SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_DARK_WARNING());
                        }
                    }
                    else if (creature.GetRace() == Race.ASMODIANS)
                    {
                        if (balance > 0)
                        {
                            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(8875 + balance, creature, creature);
                            PacketSendUtility.SendPacket((Player) creature, SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_DARK_GAIN());
                        }
                        else
                        {
                            PacketSendUtility.SendPacket((Player) creature, SmSystemMessage.STR_MSG_WEAK_RACE_BUFF_LIGHT_WARNING());
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public override void ClearLocation()
    {
        ForEachCreature(creature =>
        {
            if (IsEnemy(creature))
            {
                if (creature is Kisk kisk)
                    kisk.GetController().Die();
                else if (creature is Player player && !(player.IsStaff() && SiegeConfig.IGNORE_STAFF_ON_LOCATION_CLEAR))
                    TeleportService.MoveToBindLocation(player);
            }
        });
    }

    public enum SiegeBuffAction
    {
        ADD,
        LEAVE_ZONE_REMOVE,
        SIEGE_END_REMOVE
    }
}
