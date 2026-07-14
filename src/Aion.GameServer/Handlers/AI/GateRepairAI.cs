using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/GateRepairAI (Source).</summary>
[AIName("siege_gaterepair")]
public class GateRepairAI : NpcAI
{
    private static readonly ILogger log = NullLogger.Instance;
    private long nextActivationTime = 0;

    public GateRepairAI(Npc owner)
        : base(owner)
    {
    }

    protected new SiegeSpawnTemplate GetSpawnTemplate()
    {
        return (SiegeSpawnTemplate)base.GetSpawnTemplate();
    }

    protected override void HandleDialogStart(Player player)
    {
        DoorRepairData repairData = SiegeService.GetInstance().GetDoorRepairData(GetSpawnTemplate().GetSiegeId());
        if (repairData == null)
            return;

        RequestResponseHandler<Npc> gaterepair = new GateRepairResponseHandler(this, GetOwner(), player, repairData);
        if (player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_POPUPDIALOG, gaterepair))
            PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_POPUPDIALOG, ((SiegeNpc)GetOwner()).GetSiegeId(), GetCooldown()));
    }

    protected override void HandleDialogFinish(Player player)
    {
    }

    public void OnActivate(Player player, DoorRepairData repairData)
    {
        DoorRepairStone repairStone = GetSpawnTemplate() == null ? null : repairData.GetRepairStone(GetSpawnTemplate().GetStaticId());
        if (repairStone == null)
            return;
        if (GetPosition().GetWorldMapInstance().GetObjectByStaticId(repairStone.GetDoorId()) is Creature door)
        {
            int healValue = (int)JavaMath.Round(door.GetLifeStats().GetMaxHp() * SiegeConfig.DOOR_REPAIR_HEAL_PERCENT);
            if (door.GetLifeStats().GetCurrentHp() + healValue > door.GetLifeStats().GetMaxHp())
            {
                healValue = door.GetLifeStats().GetMaxHp() - door.GetLifeStats().GetCurrentHp();
            }

            if (LoggingConfig.LOG_SIEGE)
                log.LogInformation("Gate Repair Stone with staticId: " + GetSpawnTemplate().GetStaticId() + " siege: " + GetSpawnTemplate().GetSiegeId() + " activated by " + player + " (race: " + player.GetRace() + ") to heal door with staticId: " + (door.GetSpawn().GetStaticId()) + " by " + healValue);
            Interlocked.Exchange(ref nextActivationTime, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + repairData.GetCd());
            PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_REPAIR_ABYSS_DOOR(player.GetName(), "" + healValue));
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_ACTION_ANIMATION(GetObjectId(), ActionAnimation.REPAIR_GATE, door.GetObjectId()));
            door.GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.DOOR_REPAIR, healValue);
        }
        else
        {
            throw new SiegeException("Could not find a door to repair for siege " + GetSpawnTemplate().GetSiegeId() + " for npc_id = " + GetNpcId() + " with static_id = " + GetSpawnTemplate().GetStaticId());
        }
    }

    private int GetCooldown()
    {
        long next = Interlocked.Read(ref nextActivationTime);
        return next <= 0 ? 0 : (int)System.Math.Max(0, (next - System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
    }

    private bool CanActivate(Player player, DoorRepairData repairData)
    {
        FortressLocation loc = SiegeService.GetInstance().GetFortress(GetSpawnTemplate().GetSiegeId());
        if (GetCooldown() > 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_DOOR_REPAIR_OUT_OF_COOLTIME());
            return false;
        }
        if (loc.GetRace().GetRaceId() != player.GetRace().GetRaceId())
        {
            return false;
        }
        if (loc.GetLegionId() > 0 && (!player.IsLegionMember() || player.GetLegion().GetLegionId() != loc.GetLegionId()
                || !player.GetLegionMember().HasRights(LegionPermissionsMask.ARTIFACT)))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_DOOR_REPAIR_HAVE_NO_AUTHORITY());
        }
        else if (player.GetInventory().GetItemCountByItemId(repairData.GetItemId()) >= repairData.GetCount() && player.GetInventory().DecreaseByItemId(repairData.GetItemId(), repairData.GetCount()))
        {
            return true;
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_DOOR_REPAIR_NOT_ENOUGH_FEE(DataManager.ITEM_DATA.GetItemTemplate(repairData.GetItemId()).GetL10n(), "" + repairData.GetCount()));
        }
        return false;
    }

    private sealed class GateRepairResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly GateRepairAI ai;
        private readonly Player player;
        private readonly DoorRepairData repairData;

        public GateRepairResponseHandler(GateRepairAI ai, Npc requester, Player player, DoorRepairData repairData)
            : base(requester)
        {
            this.ai = ai;
            this.player = player;
            this.repairData = repairData;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            RequestResponseHandler<Npc> repairstone = new RepairStoneResponseHandler(ai, requester, repairData);
            if (player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_DO_YOU_ACCEPT_REPAIR, repairstone))
            {
                PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_DO_YOU_ACCEPT_REPAIR,
                        ai.GetObjectId(), 5, DataManager.ITEM_DATA.GetItemTemplate(repairData.GetItemId()).GetL10n(), repairData.GetCount()));
            }
        }
    }

    private sealed class RepairStoneResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly GateRepairAI ai;
        private readonly DoorRepairData repairData;

        public RepairStoneResponseHandler(GateRepairAI ai, Npc requester, DoorRepairData repairData)
            : base(requester)
        {
            this.ai = ai;
            this.repairData = repairData;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (ai.CanActivate(responder, repairData))
            {
                ai.OnActivate(responder, repairData);
            }
        }
    }
}
