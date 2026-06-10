using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Rift;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/RVController (ATracer, Source, Sykra) : NpcController. Rift/vortex portal controller. Map→Dictionary; Integer fields→int?; two anonymous RequestResponseHandler&lt;Npc&gt;→nested Vortex/NormalRequestHandler (capture controller); instanceof PlayerGroup→is; currentTimeMillis/1000→UtcNow…/1000; ++usedEntries kept; RiftEnum enum→extension methods. RiftEnum/services/SM_*/PlayerGroup red-tolerated.</summary>
public class RVController : NpcController
{
    private bool isMaster = false;
    private bool isVortex = false;
    private bool isVolatile = false;
    private bool isInvasion = false;
    private readonly Dictionary<int, Player> passedPlayers = new Dictionary<int, Player>();
    private SpawnTemplate slaveSpawnTemplate;
    private Npc slave;
    private int? maxEntries;
    private int? minLevel;
    private int? maxLevel;
    private int usedEntries = 0;
    private bool isAccepting;
    private RiftEnum riftTemplate;
    private int deSpawnedTime;

    /// <summary>Used to create master rifts or slave rifts (slave == null).</summary>
    public RVController(Npc slave, RiftEnum riftTemplate)
    {
        this.riftTemplate = riftTemplate;
        this.isVortex = riftTemplate.IsVortex();
        this.maxEntries = riftTemplate.GetEntries();
        this.minLevel = riftTemplate.GetMinLevel();
        this.maxLevel = riftTemplate.GetMaxLevel();
        this.deSpawnedTime = ((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000))
            + (isVortex ? VortexService.GetInstance().GetDuration() * 3600 : RiftService.GetInstance().GetDuration() * 3600);
        this.isInvasion = riftTemplate.IsInvasionRift();

        if (slave != null) // master rift should be created
        {
            this.slave = slave;
            this.slaveSpawnTemplate = slave.GetSpawn();
            isMaster = true;
            isAccepting = true;
        }
    }

    public RVController(Npc slave, RiftEnum riftTemplate, bool isWithGuards)
    {
        this.riftTemplate = riftTemplate;
        this.isVortex = riftTemplate.IsVortex();
        this.maxEntries = riftTemplate.GetEntries();
        this.minLevel = riftTemplate.GetMinLevel();
        this.maxLevel = riftTemplate.GetMaxLevel();
        this.deSpawnedTime = ((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000))
            + (isVortex ? VortexService.GetInstance().GetDuration() * 3600 : RiftService.GetInstance().GetDuration() * 3600);
        this.isInvasion = riftTemplate.IsInvasionRift();

        if (slave != null) // master rift should be created
        {
            this.slave = slave;
            slaveSpawnTemplate = slave.GetSpawn();
            isMaster = true;
            isAccepting = true;
            isVolatile = riftTemplate.CanBeVolatile() && isWithGuards;
        }
    }

    public override void OnDialogRequest(Player player)
    {
        if (isMaster || isAccepting)
        {
            if (isInvasion && player.GetOppositeRace() != riftTemplate.GetDestination())
                return;
            OnRequest(player);
        }
    }

    private void OnRequest(Player player)
    {
        if (isVortex)
        {
            RequestResponseHandler<Npc> responseHandler = new VortexRequestHandler(this, GetOwner());

            bool requested = player.GetResponseRequester().PutRequest(904304, responseHandler);
            if (requested)
            {
                PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(904304, GetOwner().GetObjectId(), 5));
            }
        }
        else
        {
            RequestResponseHandler<Npc> responseHandler = new NormalRequestHandler(this, GetOwner());

            bool requested = player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_ASK_PASS_BY_DIRECT_PORTAL, responseHandler);
            if (requested)
            {
                PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_ASK_PASS_BY_DIRECT_PORTAL, 0, 0));
            }
        }
    }

    private bool OnAccept(Player player)
    {
        if (!isAccepting)
        {
            return false;
        }

        if (!GetOwner().IsSpawned())
        {
            return false;
        }

        if (player.GetLevel() > GetMaxLevel() || player.GetLevel() < GetMinLevel())
        {
            AuditLogger.Log(player, "tried to use rift outside level restriction");
            return false;
        }

        if (isVortex && GetUsedEntries() >= GetMaxEntries())
        {
            return false;
        }

        return true;
    }

    public override void OnDespawn()
    {
        RiftInformer.SendRiftDespawn(GetOwner().GetWorldId(), GetOwner().GetObjectId());
        RiftManager.RemoveSpawnedRift(GetOwner());
        base.OnDespawn();
    }

    public bool IsMaster()
    {
        return isMaster;
    }

    public bool IsVortex()
    {
        return isVortex;
    }

    /// <summary>the maxEntries</summary>
    public int? GetMaxEntries()
    {
        return maxEntries;
    }

    /// <summary>the minLevel</summary>
    public int? GetMinLevel()
    {
        return minLevel;
    }

    /// <summary>the maxLevel</summary>
    public int? GetMaxLevel()
    {
        return maxLevel;
    }

    /// <summary>the riftTemplate</summary>
    public RiftEnum GetRiftTemplate()
    {
        return riftTemplate;
    }

    /// <summary>slave rift</summary>
    public Npc GetSlave()
    {
        return slave;
    }

    /// <summary>the usedEntries</summary>
    public int GetUsedEntries()
    {
        return usedEntries;
    }

    public int GetRemainTime()
    {
        return deSpawnedTime - (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    public bool IsVolatile()
    {
        return isVolatile;
    }

    public bool IsInvasion()
    {
        return isInvasion;
    }

    public Dictionary<int, Player> GetPassedPlayers()
    {
        return passedPlayers;
    }

    public void SyncPassed(bool invasion)
    {
        usedEntries = invasion ? passedPlayers.Count : ++usedEntries;
        RiftInformer.SendRiftInfo(GetWorldsList(this));
    }

    private int[] GetWorldsList(RVController controller)
    {
        int first = controller.GetOwner().GetWorldId();
        if (controller.IsMaster())
        {
            return new int[] { first, controller.slaveSpawnTemplate.GetWorldId() };
        }
        return new int[] { first };
    }

    // Java parity: anonymous RequestResponseHandler<Npc> in onRequest (vortex branch).
    private sealed class VortexRequestHandler : RequestResponseHandler<Npc>
    {
        private readonly RVController c;

        public VortexRequestHandler(RVController c, Npc npc) : base(npc)
        {
            this.c = c;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (c.OnAccept(responder))
            {
                if (responder.IsInTeam())
                {
                    if (responder.GetCurrentTeam() is PlayerGroup)
                    {
                        PlayerGroupService.RemovePlayer(responder);
                    }
                    else
                    {
                        PlayerAllianceService.RemovePlayer(responder);
                    }
                }

                VortexLocation loc = VortexService.GetInstance().GetLocationByRift(requester.GetNpcId());
                TeleportService.TeleportTo(responder, loc.GetStartPoint());

                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_MSG_INVADE_DIRECT_PORTAL_OPEN_NOTICE());

                // Update passed players count
                c.passedPlayers[responder.GetObjectId()] = responder;
                c.SyncPassed(true);
            }
        }
    }

    // Java parity: anonymous RequestResponseHandler<Npc> in onRequest (normal branch).
    private sealed class NormalRequestHandler : RequestResponseHandler<Npc>
    {
        private readonly RVController c;

        public NormalRequestHandler(RVController c, Npc npc) : base(npc)
        {
            this.c = c;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (c.OnAccept(responder))
            {
                int worldId = c.slaveSpawnTemplate.GetWorldId();
                float x = c.slaveSpawnTemplate.GetX();
                float y = c.slaveSpawnTemplate.GetY();
                float z = c.slaveSpawnTemplate.GetZ();

                TeleportService.TeleportTo(responder, worldId, x, y, z);
                // Update passed players count
                c.SyncPassed(false);
            }
        }
    }
}
