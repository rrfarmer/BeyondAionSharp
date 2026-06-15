using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("empowered_agent")]
public class EmpoweredAgent : AbstractSiegeProtectorAI, HpPhases.PhaseHandler
{
    private readonly List<int> guardIds = new List<int>();
    private readonly HpPhases hpPhases = new HpPhases(80, 70, 60, 50, 40, 30, 25, 20, 5);
    private readonly AtomicBoolean isWalkingCompleted = new AtomicBoolean();
    private bool canThink = true;
    private Npc flagNpc;
    private ScheduledTask activationTask, aggroResetTask;

    public EmpoweredAgent(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21779, 1, GetOwner()).UseNoAnimationSkill();
        canThink = false;
        EmoteManager.EmoteStopAttacking(GetOwner());
        switch (GetOwner().GetNpcId())
        {
            case 235064:
                guardIds.AddRange(new[] { 235334, 235335, 235336, 235337, 235338, 235339 });
                StartWalking("600100000_npcpathgod_l");
                break;
            case 235065:
                guardIds.AddRange(new[] { 235340, 235341, 235342, 235343, 235344, 235345 });
                StartWalking("600100000_npcpathgod_d");
                break;
        }
    }

    private void StartWalking(string walkerId)
    {
        GetOwner().GetSpawn().SetWalkerId(walkerId);
        WalkManager.StartWalking(this);
        GetOwner().SetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 80:
            case 70:
            case 60:
            case 50:
            case 40:
            case 30:
            case 20:
                OnGuardSpawnEvent();
                break;
            case 25:
            case 5:
                GetOwner().QueueSkill(21778, 1, 3000, NpcSkillTargetAttribute.ME);
                break;
        }
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().GetCurrentStep().IsLastStep() && IsDestinationReached() && isWalkingCompleted.CompareAndSet(false, true))
            OnDestinationArrived();
    }

    private void OnDestinationArrived()
    {
        GetSpawnTemplate().SetWalkerId(null);
        WalkManager.StopWalking(this);
        SpawnFlag();
        TryActivating();
    }

    private void TryActivating()
    {
        Npc otherAgent = GetOtherAgent();
        if (otherAgent != null && PositionUtil.GetDistance(GetOwner(), otherAgent, false) <= 5)
        {
            GetOwner().GetEffectController().RemoveEffect(21779);
            GetOwner().GetLifeStats().SetCurrentHpPercent(100);
            GetAggroList().AddHate(otherAgent, 100_000_000);
            StartFight(otherAgent);
        }
        else
        {
            activationTask = ThreadPoolManager.GetInstance().Schedule(() => TryActivating(), 5000L);
        }
    }

    private Npc GetOtherAgent()
    {
        return GetOwner().GetNpcId() switch
        {
            235064 => GetOwner().GetPosition().GetWorldMapInstance().GetNpc(235065), // Veille
            235065 => GetOwner().GetPosition().GetWorldMapInstance().GetNpc(235064), // Mastarius
            _ => null,
        };
    }

    private void StartFight(Npc otherAgent)
    {
        canThink = true;
        GetOwner().GetGameStats().ResetFightStats();
        GetOwner().SetTarget(otherAgent);
        SetStateIfNot(AIState.FIGHT);
        Think();
        GetOwner().SetState(CreatureState.ACTIVE, true);
        EmoteManager.EmoteStartAttacking(GetOwner(), otherAgent);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        aggroResetTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { ResetPlayerHate(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(15000));
    }

    private void ResetPlayerHate()
    {
        foreach (var info in GetAggroList().Stream().Where(info => info.GetAttacker().GetMaster() is Player).ToList())
            info.SetHate(0);
    }

    private void SpawnFlag()
    {
        if (flagNpc != null)
        {
            NullLogger.Instance.LogWarning(new Exception(), "Tried to spawn flag for empowered agent {NpcId} twice!", GetNpcId());
            return;
        }
        int flagNpcId = GetNpcId() switch
        {
            235064 => 832830, // Veille
            235065 => 832831, // Mastarius
            _ => 0,
        };
        if (flagNpcId != 0)
        {
            WorldPosition pos = GetPosition();
            flagNpc = (Npc)Spawn(flagNpcId, pos.GetX(), pos.GetY(), pos.GetZ(), unchecked((sbyte)pos.GetHeading()));
        }
    }

    private void DespawnFlag()
    {
        if (flagNpc != null)
        {
            flagNpc.GetController().Delete();
            flagNpc = null;
        }
    }

    private void OnGuardSpawnEvent()
    {
        int worldId = GetOwner().GetWorldId();
        float guardAmount = GetAggroList().Stream().Where(aggroInfo => aggroInfo.GetHate() > 0).Count() / 2.5f;
        if (guardAmount < 6)
            guardAmount = 6;
        for (int i = 0; i < guardAmount; i++)
        {
            Point3D pos = GetRndPos();
            SiegeSpawnTemplate template = SpawnEngine.SpawnEngine.NewSiegeSpawn(worldId, Rnd.Get(guardIds), GetSpawnTemplate().GetSiegeId(), SiegeRace.BALAUR, SiegeModType.SIEGE, pos.GetX(),
                pos.GetY(), pos.GetZ(), (byte)0);
            SpawnEngine.SpawnEngine.SpawnObject(template, 1);
        }
    }

    private Point3D GetRndPos()
    {
        double angleRadians = (Rnd.NextFloat(360f)) * Math.PI / 180.0;
        float distance = Rnd.NextFloat(10f);
        float x1 = (float)(Math.Cos(angleRadians) * distance);
        float y1 = (float)(Math.Sin(angleRadians) * distance);
        WorldPosition p = GetPosition();
        return new Point3D(p.GetX() + x1, p.GetY() + y1, p.GetZ());
    }

    private void CancelActivationTask()
    {
        if (activationTask != null && !activationTask.IsDone())
            activationTask.Cancel(true);
        if (aggroResetTask != null && !aggroResetTask.IsDone())
            aggroResetTask.Cancel(true);
    }

    protected override void HandleDied()
    {
        DespawnFlag();
        CancelActivationTask();
        ((AgentSiege)GetSiege()).SetWinnerRace(GetObjectTemplate().GetRace() == Race.GHENCHMAN_LIGHT ? SiegeRace.ASMODIANS : SiegeRace.ELYOS);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        DespawnFlag();
        CancelActivationTask();
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_RESPAWN or AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE => false,
            _ => base.Ask(question),
        };
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(SiegeConfig.FORTRESS_PROTECTOR_HEALTH_MULTIPLIER);
    }
}
