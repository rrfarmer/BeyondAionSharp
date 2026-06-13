using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.Teleport;

/// <summary>Java parity: services/teleport/TeleportService (xTz, Neon) — all-static teleport hub. Many teleportTo overloads, flight/instant sendLoc, dead/prison/npc/bind/instance-exit/event/scroll/channel teleports, obelisk/kisk bind packets. Idioms: double[] eventPos statics; FutureTask&lt;Void&gt;(spawnTask,null)→FutureTask&lt;object&gt; (concurrency shim red-tolerated); anonymous RequestResponseHandler→nested TeleportRequestHandler; SpawnTask nested (accesses outer private statics); Math.toRadians→*PI/180; Float.isNaN→float.IsNaN; byte-heading bit math preserved; HiPass effect lambda; .equals→==. World/Instance/DAO/packets red-tolerated.</summary>
public class TeleportService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(TeleportService));
    private static double[] eventPosAsmodians;
    private static double[] eventPosElyos;

    public static void TeleportToFirstTeleportLocation(Player player, Npc teleporter, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        TeleporterTemplate teleporterTemplate = ValidateTeleporterAndGetTemplate(player, teleporter);
        if (teleporterTemplate == null)
            return;
        Teleport(player, teleporterTemplate.GetTeleLocIdData().GetTelelocations()[0], animation);
    }

    public static void Teleport(Player player, TeleportLocation location, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        TelelocationTemplate locationTemplate = DataManager.TELELOCATION_DATA.GetTelelocationTemplate(location.GetLocId());
        if (locationTemplate == null)
        {
            log.LogWarning("Missing teleloc_template in teleport_location.xml with locId {LocId}", location.GetLocId());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return;
        }

        // TODO: remove teleportation route if it's enemy fortress (1221, 1231, 1241)
        int id = SiegeService.GetInstance().GetSiegeIdByLocId(location.GetLocId());
        if (id > 0 && !SiegeService.GetInstance().GetSiegeLocation(id).IsCanTeleport(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return;
        }
        if (location.GetRequiredQuest() != 0 && !player.IsCompleteQuest(location.GetRequiredQuest()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NEED_FINISH_QUEST());
            return;
        }

        if (!CheckKinahForTransportation(location, player))
            return;

        if (location.GetType_() == TeleportType.FLIGHT)
        {
            if (SecurityConfig.ENABLE_FLYPATH_VALIDATOR)
            {
                FlyPathEntry flypath = DataManager.FLY_PATH.GetPathTemplate(location.GetLocId());
                if (flypath == null)
                {
                    AuditLogger.Log(player, "tried to use invalid flyPath #" + location.GetLocId());
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
                    return;
                }

                double dist = PositionUtil.GetDistance(player, flypath.GetStartX(), flypath.GetStartY(), flypath.GetStartZ());
                if (dist > 7)
                {
                    AuditLogger.Log(player, "tried to use flyPath #" + location.GetLocId() + " but he's too far " + dist);
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
                    return;
                }

                if (player.GetWorldId() != flypath.GetStartWorldId())
                {
                    AuditLogger.Log(player, "tried to use flyPath #" + location.GetLocId() + " from invalid start world " + player.GetWorldId()
                        + ", expected " + flypath.GetStartWorldId());
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
                    return;
                }

                player.SetCurrentFlypath(flypath);
            }
            player.UnsetPlayerMode(PlayerMode.RIDE);
            player.SetState(CreatureState.FLYING);
            player.UnsetState(CreatureState.ACTIVE);
            player.SetFlightTeleportId(location.GetTeleportId());
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, location.GetTeleportId(), 0), true);
        }
        else
        {
            int instanceId = 1;
            int mapId = locationTemplate.GetMapId();
            if (player.GetWorldId() == mapId)
            {
                instanceId = player.GetInstanceId();
            }
            SendLoc(player, mapId, instanceId, locationTemplate.GetX(), locationTemplate.GetY(), locationTemplate.GetZ(),
                (byte)locationTemplate.GetHeading(), animation);
        }
    }

    public static TeleporterTemplate ValidateTeleporterAndGetTemplate(Player player, Npc teleporter)
    {
        TeleporterTemplate template = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(teleporter.GetNpcId());
        if (template == null)
        {
            AuditLogger.Log(player, "tried to use invalid teleporter " + teleporter + " (no teleporter data) at " + player.GetPosition());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_WRONG_NPC());
            return null;
        }
        CreatureType creatureType = teleporter.GetType_(player);
        if (creatureType != CreatureType.FRIEND && creatureType != CreatureType.SUPPORT)
        {
            AuditLogger.Log(player, "tried to use invalid teleporter " + teleporter + " (wrong race) at " + player.GetPosition());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_WRONG_NPC());
            return null;
        }
        if (!PositionUtil.IsInTalkRange(player, teleporter))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_FAR_FROM_NPC());
            return null;
        }
        if (player.IsInFlyingState())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_AIRPORT_WHEN_FLYING());
            return null;
        }
        return template;
    }

    private static bool CheckKinahForTransportation(TeleportLocation location, Player player)
    {
        Storage inventory = player.GetInventory();

        long transportationPrice;

        // If HiPassEffect is active, then all flight/teleport prices are 1 kinah
        if (player.GetEffectController().HasAbnormalEffect(e => e.IsHiPass()))
            transportationPrice = 1;
        else
        {
            int basePrice = location.GetPrice();
            // TODO check for location.getPricePvp()
            transportationPrice = Aion.GameServer.Services.Trade.PricesService.GetPriceForService(basePrice, player.GetRace());
        }

        if (!inventory.TryDecreaseKinah(transportationPrice, ItemUpdateType.DEC_KINAH_FLY))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(transportationPrice));
            return false;
        }
        return true;
    }

    private static void SendLoc(Player player, int worldId, int instanceId, float x, float y, float z, byte h, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        AbortPlayerActions(player);
        // despawn from world and send animation to others (also ends flying)
        World.World.GetInstance().Despawn(player, animation.GetDefaultObjectDeleteAnimation());

        SpawnTask spawnTask = new SpawnTask(player, worldId, instanceId, x, y, z, h, animation);
        if (animation == Aion.GameServer.Model.Animations.TeleportAnimation.NONE) // instant teleport (don't wait for player fade-out)
            spawnTask.Run();
        else
        {
            // send teleport animation to player and trigger CM_TELEPORT_ANIMATION_DONE when the animation ended
            PacketSendUtility.SendPacket(player, new SM_TELEPORT_LOC(worldId, instanceId, x, y, z, h, animation));
            // task will be triggered from CM_TELEPORT_ANIMATION_DONE
            player.GetController().AddTask(TaskId.TELEPORT, new FutureTask<object>(spawnTask, null));
        }
    }

    private static void AbortPlayerActions(Player player)
    {
        if (player.HasStore())
            PrivateStoreService.ClosePrivateStore(player);
        player.GetController().CancelCurrentSkill(null);
        player.SetTarget(null);
        player.UnsetPlayerMode(PlayerMode.RIDE);
    }

    private static void SpawnOnSameMap(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_CHANNEL_INFO(player.GetPosition()));
        PacketSendUtility.SendPacket(player, new SM_PLAYER_INFO(player));
        PacketSendUtility.SendPacket(player, new SM_STATS_INFO(player));
        PacketSendUtility.SendPacket(player, new SM_MOTION(player.GetObjectId(), player.GetMotions().GetActiveMotions()));
        World.World.GetInstance().Spawn(player);
        World.World.GetInstance().Spawn(player.GetPet());
        player.GetController().StartProtectionActiveTask();
        player.GetEffectController().UpdatePlayerEffectIcons(null);
        player.GetController().UpdateZone();
        player.SetPortAnimation(ArrivalAnimation.NONE);
    }

    public static void TeleportTo(Player player, WorldPosition pos)
    {
        if (player.GetWorldId() == pos.GetMapId())
        {
            World.World.GetInstance().SetPosition(player.GetPet(), pos.GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
            World.World.GetInstance().SetPosition(player, pos.GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
            SpawnOnSameMap(player);
        }
        else if (player.IsDead())
        {
            TeleportDeadTo(player, pos.GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
        }
        else
        {
            TeleportTo(player, pos.GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading(), Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
        }
    }

    public static void TeleportDeadTo(Player player, int worldId, int instanceId, float x, float y, float z, byte heading)
    {
        if (player.GetWorldId() != worldId || player.GetInstanceId() != instanceId)
        {
            ConquerorAndProtectorService.GetInstance().OnLeaveMap(player);
            InstanceService.OnLeaveInstance(player);
        }
        World.World.GetInstance().SetPosition(player, worldId, instanceId, x, y, z, heading);
        PacketSendUtility.SendPacket(player, new SM_CHANNEL_INFO(player.GetPosition()));
        PacketSendUtility.SendPacket(player, new SM_PLAYER_SPAWN(player));
        player.SetPortAnimation(ArrivalAnimation.LANDING);
        PacketSendUtility.SendPacket(player, new SM_PLAYER_INFO(player));

        if (player.IsLegionMember() && player.GetLegionMember().GetWorldId() != worldId)
            LegionService.GetInstance().UpdateMemberInfo(player);
    }

    public static void TeleportTo(Player player, int worldId, float x, float y, float z)
    {
        TeleportTo(player, worldId, x, y, z, player.GetHeading(), Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, int worldId, float x, float y, float z, byte h)
    {
        TeleportTo(player, worldId, x, y, z, h, Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, int worldId, float x, float y, float z, byte h, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        TeleportTo(player, worldId, player.GetWorldId() != worldId ? 1 : player.GetInstanceId(), x, y, z, h, animation);
    }

    // Java parity: house/bind coordinate getters return boxed Float (auto-unboxed at the call); accept float?.
    public static void TeleportTo(Player player, int worldId, float? x, float? y, float? z, byte h, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        TeleportTo(player, worldId, x ?? 0, y ?? 0, z ?? 0, h, animation);
    }

    public static void TeleportTo(Player player, int worldId, int instanceId, float x, float y, float z)
    {
        TeleportTo(player, worldId, instanceId, x, y, z, player.GetHeading(), Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, int worldId, int instanceId, float x, float y, float z, byte h)
    {
        TeleportTo(player, worldId, instanceId, x, y, z, h, Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, WorldMapInstance instance, float x, float y, float z)
    {
        TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), x, y, z, player.GetHeading(), Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, WorldMapInstance instance, float x, float y, float z, byte h)
    {
        TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), x, y, z, h, Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    public static void TeleportTo(Player player, WorldMapInstance instance, float x, float y, float z, byte h, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), x, y, z, h, animation);
    }

    public static void TeleportTo(Player player, int worldId, int instanceId, float x, float y, float z,
        byte heading, Aion.GameServer.Model.Animations.TeleportAnimation animation)
    {
        if (player.IsDead())
        {
            PlayerReviveService.Revive(player, 20, 20, true, 0);
        }
        else if (DuelService.GetInstance().IsDueling(player))
        {
            DuelService.GetInstance().LoseDuel(player);
        }
        SendLoc(player, worldId, instanceId, x, y, z, heading, animation);
    }

    public static void ShowMap(Player player, Npc npc)
    {
        TeleporterTemplate template = ValidateTeleporterAndGetTemplate(player, npc);
        if (template != null)
            PacketSendUtility.SendPacket(player, new SM_TELEPORT_MAP(npc.GetObjectId(), template.GetTeleportId()));
    }

    public static void TeleportToPrison(Player player)
    {
        if (player.GetRace() == Race.ELYOS)
            TeleportTo(player, WorldMapType.LF_PRISON.GetId(), 275, 239, 49);
        else if (player.GetRace() == Race.ASMODIANS)
            TeleportTo(player, WorldMapType.DF_PRISON.GetId(), 275, 239, 49);
    }

    public static void TeleportToNpc(Player player, int npcId)
    {
        SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(player.GetWorldId(), npcId);

        if (searchResult == null)
        {
            log.LogWarning("No npc spawn found for : " + npcId);
            return;
        }

        SpawnSpotTemplate spot = searchResult.GetSpot();
        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        float npcRadius = npcTemplate == null ? 1 : npcTemplate.GetBoundRadius().GetFront(); // StaticObject has no npcTemplate since it's no npc
        WorldMapInstance instance;
        if (player.GetWorldId() == searchResult.GetWorldId())
            instance = player.GetPosition().GetWorldMapInstance();
        else if (World.World.GetInstance().GetWorldMap(searchResult.GetWorldId()).IsInstanceType())
            instance = InstanceService.GetOrRegisterInstance(searchResult.GetWorldId(), player);
        else
            instance = World.World.GetInstance().GetWorldMap(searchResult.GetWorldId()).GetMainWorldMapInstance();

        // calculate position 1m in front of the npc
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(spot.GetHeading());
        float x = spot.GetX() + (float)Math.Cos(radian) * (1f + npcRadius);
        float y = spot.GetY() + (float)Math.Sin(radian) * (1f + npcRadius);
        float z = GeoService.GetInstance().GetZ(searchResult.GetWorldId(), x, y, spot.GetZ(), instance.GetInstanceId());
        if (float.IsNaN(z)) // no collision found or geo disabled
            z = spot.GetZ() + 0.5f;
        byte heading = (byte)((spot.GetHeading() & 0xFF) >= 60 ? spot.GetHeading() - 60 : spot.GetHeading() + 60); // look towards npc

        TeleportTo(player, instance, x, y, z, heading, Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
    }

    /// <summary>This method will send the set bind point packet</summary>
    public static void SendObeliskBindPoint(Player player)
    {
        int worldId;
        float x, y, z;
        if (player.GetBindPoint() != null)
        {
            BindPointPosition bplist = player.GetBindPoint();
            worldId = bplist.GetMapId();
            x = bplist.GetX();
            y = bplist.GetY();
            z = bplist.GetZ();
        }
        else
        {
            PlayerInitialData.LocationData locationData = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
            worldId = locationData.GetMapId();
            x = locationData.GetX();
            y = locationData.GetY();
            z = locationData.GetZ();
        }
        PacketSendUtility.SendPacket(player, new SM_BIND_POINT_INFO(worldId, x, y, z));
    }

    public static void SendKiskBindPoint(Player player)
    {
        if (player.GetKisk() != null)
            PacketSendUtility.SendPacket(player, new SM_BIND_POINT_INFO(player.GetKisk()));
    }

    public static void MoveToBindLocation(Player player)
    {
        float x, y, z;
        int worldId;
        byte h;

        if (player.GetBindPoint() != null)
        {
            BindPointPosition bplist = player.GetBindPoint();
            worldId = bplist.GetMapId();
            x = bplist.GetX();
            y = bplist.GetY();
            z = bplist.GetZ();
            h = bplist.GetHeading();
        }
        else
        {
            PlayerInitialData.LocationData locationData = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
            worldId = locationData.GetMapId();
            x = locationData.GetX();
            y = locationData.GetY();
            z = locationData.GetZ();
            h = locationData.GetHeading();
        }
        TeleportTo(player, worldId, x, y, z, h);
    }

    public static void MoveToTargetWithDistance(VisibleObject obj, Player player, int direction, int distance)
    {
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(obj.GetHeading());
        float x0 = obj.GetX();
        float y0 = obj.GetY();
        float x1 = (float)(Math.Cos(Math.PI * direction + radian) * distance);
        float y1 = (float)(Math.Sin(Math.PI * direction + radian) * distance);
        TeleportTo(player, obj.GetWorldId(), x0 + x1, y0 + y1, obj.GetZ());
    }

    public static void MoveToInstanceExit(Player player, int worldId, Race race)
    {
        InstanceExit instanceExit = DataManager.INSTANCE_EXIT_DATA.GetInstanceExit(worldId, race);
        if (instanceExit != null && InstanceService.InstanceExists(instanceExit.GetExitWorld(), 1))
        {
            TeleportTo(player, instanceExit.GetExitWorld(), instanceExit.GetX(), instanceExit.GetY(), instanceExit.GetZ(), instanceExit.GetH());
        }
        else
        {
            if (instanceExit == null)
                log.LogWarning("No instance exit found for race: " + race + " " + worldId);
            MoveToBindLocation(player);
        }
    }

    public static void UseTeleportScroll(Player player, string portalName, int worldId)
    {
        PortalScroll template = DataManager.PORTAL2_DATA.GetPortalScroll(portalName);
        if (template == null)
        {
            log.LogWarning("No portal template found for: " + portalName + " " + worldId);
            return;
        }

        Race playerRace = player.GetRace();
        PortalPath portalPath = template.GetPortalPath();
        if (portalPath == null)
        {
            log.LogWarning("No portal scroll for " + playerRace + " on: " + portalName + " " + worldId);
            return;
        }
        PortalLoc loc = DataManager.PORTAL_LOC_DATA.GetPortalLoc(portalPath.GetLocId());
        if (loc == null)
        {
            log.LogWarning("No portal loc for locId " + portalPath.GetLocId());
            return;
        }
        TeleportTo(player, worldId, loc.GetX(), loc.GetY(), loc.GetZ());
    }

    public static void ChangeChannel(Player player, int channel)
    {
        World.World.GetInstance().SetPosition(player, player.GetWorldId(), channel + 1, player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
        player.GetController().StartProtectionActiveTask();
        PacketSendUtility.SendPacket(player, new SM_CHANNEL_INFO(player.GetPosition()));
        PacketSendUtility.SendPacket(player, new SM_PLAYER_SPAWN(player));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TELEPORT_ZONECHANNEL(channel));
    }

    public static void SetEventPos(WorldPosition pos, Race race)
    {
        if (race == Race.ELYOS)
        {
            eventPosElyos = new double[] { pos.GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading() };
            log.LogInformation("elyos: mapId: " + pos.GetMapId() + ", instanceId: " + (int)eventPosElyos[1] + ", X: " + eventPosElyos[2] + ", Y: " + eventPosElyos[3]
                + ", Z: " + eventPosElyos[4] + ", H: " + (byte)eventPosElyos[5]);
        }
        else if (race == Race.ASMODIANS)
        {
            eventPosAsmodians = new double[] { pos.GetWorldMapInstance().GetMapId(), pos.GetInstanceId(), pos.GetX(), pos.GetY(), pos.GetZ(),
                pos.GetHeading() };
            log.LogInformation("asmo: mapId: " + pos.GetMapId() + ", instanceId: " + (int)eventPosAsmodians[1] + ", X: " + eventPosAsmodians[2] + ", Y: "
                + eventPosAsmodians[3] + ", Z: " + eventPosAsmodians[4] + ", H: " + (byte)eventPosAsmodians[5]);
        }
    }

    public static void TeleportToEvent(Player player)
    {
        double[] pos = null;
        if (player.GetRace() == Race.ELYOS)
            pos = eventPosElyos;
        else if (player.GetRace() == Race.ASMODIANS)
            pos = eventPosAsmodians;

        if (pos == null)
            MoveToBindLocation(player);
        else
            TeleportTo(player, (int)pos[0], (int)pos[1], (float)pos[2], (float)pos[3], (float)pos[4], (byte)pos[5], Aion.GameServer.Model.Animations.TeleportAnimation.FADE_OUT_BEAM);
    }

    /// <summary>Sends a teleport request to the player. He will only be teleported to the Npc if he accepts the request. Returns true if the request was sent.</summary>
    public static bool SendTeleportRequest(Player player, int npcId)
    {
        int questionMsgId = 905097; // You will be teleported to %0 Continue?
        RequestResponseHandler<Creature> handler = new TeleportRequestHandler(npcId);

        if (!player.GetResponseRequester().PutRequest(questionMsgId, handler))
            return false;
        PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(questionMsgId, 0, 0, DataManager.NPC_DATA.GetNpcTemplate(npcId).GetL10n()));
        return true;
    }

    private sealed class TeleportRequestHandler : RequestResponseHandler<Creature>
    {
        private readonly int npcId;

        public TeleportRequestHandler(int npcId)
            : base(null)
        {
            this.npcId = npcId;
        }

        public override void AcceptRequest(Creature requester, Player responder)
        {
            TeleportToNpc(responder, npcId);
        }
    }

    private class SpawnTask
    {
        private readonly Player player;
        private readonly int worldId, instanceId;
        private readonly float x, y, z;
        private readonly byte h;
        private readonly Aion.GameServer.Model.Animations.TeleportAnimation animation;

        public SpawnTask(Player player, int worldId, int instanceId, float x, float y, float z, byte h, Aion.GameServer.Model.Animations.TeleportAnimation animation)
        {
            this.player = player;
            this.worldId = worldId;
            this.instanceId = instanceId;
            this.x = x;
            this.y = y;
            this.z = z;
            this.h = h;
            this.animation = animation;
        }

        public void Run()
        {
            if (player.IsSpawned())
                return;

            if (animation != Aion.GameServer.Model.Animations.TeleportAnimation.NONE)
            { // this is a delayed teleport (triggered after animation end)
                if (player.IsDead() || !InstanceService.InstanceExists(worldId, instanceId))
                { // instance might be destroyed after animation end if unlucky
                    PacketSendUtility.SendPacket(player, new SM_PLAYER_INFO(player));
                    World.World.GetInstance().Spawn(player);
                    return;
                }
                AbortPlayerActions(player);
            }

            int currentWorldId = player.GetWorldId();
            int currentInstance = player.GetInstanceId();
            if (currentWorldId != worldId || currentInstance != instanceId)
            {
                ConquerorAndProtectorService.GetInstance().OnLeaveMap(player);
                InstanceService.OnLeaveInstance(player);
            }
            World.World.GetInstance().SetPosition(player, worldId, instanceId, x, y, z, h);
            World.World.GetInstance().SetPosition(player.GetPet(), worldId, instanceId, x, y, z, h);

            player.SetPortAnimation(animation.GetDefaultArrivalAnimation());
            if (currentWorldId == worldId && currentInstance == instanceId)
            {
                // instant teleport when map is the same
                SpawnOnSameMap(player);
            }
            else
            {
                // teleport with full map reloading, player will spawn via CM_LEVEL_READY
                PacketSendUtility.SendPacket(player, new SM_CHANNEL_INFO(player.GetPosition()));
                PacketSendUtility.SendPacket(player, new SM_PLAYER_SPAWN(player));
                if (DataManager.WORLD_MAPS_DATA.GetTemplate(worldId).IsInstance() && !WorldMapTypeExtensions.GetWorld(worldId).IsPersonal())
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_DUNGEON_OPENED_FOR_SELF(worldId));
            }
            if (player.IsLegionMember() && player.GetLegionMember().GetWorldId() != worldId)
                LegionService.GetInstance().UpdateMemberInfo(player);
        }
    }
}
