using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Vortex;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Vortex;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Model.Vortex;

/// <summary>Java parity: model/vortex/VortexLocation (Source). implements ZoneHandler; instanceof→is; Map.put/remove→[]=/Remove; Race.equals→==; anonymous Runnable→Schedule(ct=>{...;return ValueTask.CompletedTask;}, TimeSpan). DimensionalVortex/RVController/InvasionZoneInstance/ZoneHandler/Kisk/VortexTemplate/WorldPosition red-tolerated.</summary>
public class VortexLocation : ZoneHandler
{
    private bool isActive;
    private DimensionalVortex<VortexLocation> activeVortex;
    private RVController vortexController;
    private readonly VortexTemplate template;
    private readonly List<InvasionZoneInstance> zones = new();
    private readonly Dictionary<int, Player> players = new();
    private readonly Dictionary<int, Kisk> kisks = new();
    private readonly List<VisibleObject> spawned = new();

    public VortexLocation(VortexTemplate template)
    {
        this.template = template;
    }

    public bool IsActive()
    {
        return isActive;
    }

    public void SetActiveVortex(DimensionalVortex<VortexLocation> vortex)
    {
        isActive = vortex != null;
        this.activeVortex = vortex;
    }

    public DimensionalVortex<VortexLocation> GetActiveVortex()
    {
        return activeVortex;
    }

    public void SetVortexController(RVController controller)
    {
        this.vortexController = controller;
    }

    public RVController GetVortexController()
    {
        return vortexController;
    }

    public VortexTemplate GetTemplate()
    {
        return template;
    }

    public WorldPosition GetHomePoint()
    {
        return template.GetHomePoint().GetHomePoint();
    }

    public WorldPosition GetResurrectionPoint()
    {
        return template.GetResurrectionPoint().GetResurrectionPoint();
    }

    public WorldPosition GetStartPoint()
    {
        return template.GetStartPoint().GetStartPoint();
    }

    public int GetId()
    {
        return template.GetId();
    }

    public Race GetDefendersRace()
    {
        return template.GetDefendersRace();
    }

    public Race GetInvadersRace()
    {
        return template.GetInvadersRace();
    }

    public int GetHomeWorldId()
    {
        return template.GetHomePoint().GetWorldId();
    }

    public int GetInvasionWorldId()
    {
        return template.GetStartPoint().GetWorldId();
    }

    public List<VisibleObject> GetSpawned()
    {
        return spawned;
    }

    public Dictionary<int, Player> GetPlayers()
    {
        return players;
    }

    public Dictionary<int, Kisk> GetInvadersKisks()
    {
        return kisks;
    }

    public bool IsInvaderInside(int objId)
    {
        return IsActive() && GetVortexController().GetPassedPlayers().ContainsKey(objId);
    }

    public bool IsInsideActiveVotrex(Player player)
    {
        return IsActive() && IsInsideLocation(player);
    }

    public void AddZone(InvasionZoneInstance zone)
    {
        zones.Add(zone);
        zone.AddHandler(this);
    }

    public bool IsInsideLocation(Creature creature)
    {
        if (zones.Count != 0)
        {
            foreach (InvasionZoneInstance zone in zones)
            {
                if (zone.IsInsideCreature(creature))
                    return true;
            }
        }
        return false;
    }

    public List<InvasionZoneInstance> GetZones()
    {
        return zones;
    }

    public void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Kisk)
        {
            if (creature.GetRace() == GetInvadersRace())
            {
                kisks[creature.GetObjectId()] = (Kisk)creature;
            }
        }
        else if (creature is Player)
        {
            Player player = (Player)creature;

            if (!players.ContainsKey(player.GetObjectId()))
            {
                players[player.GetObjectId()] = player;

                if (IsActive())
                {
                    if (player.GetRace() == GetInvadersRace())
                    {
                        if (GetVortexController().GetPassedPlayers().ContainsKey(player.GetObjectId())
                            && !GetActiveVortex().GetInvaders().ContainsKey(player.GetObjectId()))
                        {
                            GetActiveVortex().AddPlayer(player, true);
                        }
                    }
                    else
                    {
                        GetActiveVortex().UpdateDefenders(player);
                    }
                }
            }
        }
    }

    public void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (!IsInsideLocation(creature))
        {
            if (creature is Kisk)
            {
                kisks.Remove(creature.GetObjectId());
            }
            else if (creature is Player)
            {
                Player player = (Player)creature;

                players.Remove(player.GetObjectId());

                if (IsActive())
                {
                    if (player.GetRace() == GetInvadersRace())
                    {
                        if (GetVortexController().GetPassedPlayers().ContainsKey(player.GetObjectId()))
                        {
                            // You have left the battlefield.
                            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(904305));

                            // start kick timer
                            ThreadPoolManager.GetInstance().Schedule(ct =>
                            {
                                if (player.IsOnline() && !IsInsideActiveVotrex(player))
                                {
                                    GetActiveVortex().KickPlayer(player, true);
                                }
                                return ValueTask.CompletedTask;
                            }, System.TimeSpan.FromMilliseconds(10 * 1000));
                        }
                    }
                    else
                    {
                        // start kick timer
                        ThreadPoolManager.GetInstance().Schedule(ct =>
                        {
                            if (player.IsOnline() && !IsInsideActiveVotrex(player))
                            {
                                GetActiveVortex().KickPlayer(player, false);
                            }
                            return ValueTask.CompletedTask;
                        }, System.TimeSpan.FromMilliseconds(10 * 1000));
                    }
                }
            }
        }
    }
}
