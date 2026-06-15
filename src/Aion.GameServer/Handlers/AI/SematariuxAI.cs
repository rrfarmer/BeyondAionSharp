using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/worlds/inggison/SematariuxAI (@author Estrayl).
/// </summary>
[AIName("sematariux")]
public class SematariuxAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(90, 70, 50, 30, 20, 10, 5);
    private readonly AtomicInteger deadThunderShields = new AtomicInteger();
    private readonly AtomicBoolean isEggEventActive = new AtomicBoolean();
    // private long shieldRemovalStamp;

    public SematariuxAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19186, GetOwner(), GetOwner()); return ValueTask.CompletedTask; }, 3000L);
        SpawnShieldNpcs();
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        switch (stat.GetStat()) // Tweak for 12p (600 s | 3000 dps)
        {
            case StatEnum.MAXHP:
                stat.SetBase(20_880_000);
                break;
            case StatEnum.PHYSICAL_ATTACK:
                stat.SetBase(2200);
                break;
        }
    }

    private void SpawnShieldNpcs()
    {
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 101.31f, 2136.59f, 441.456f, (byte)0));
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 117.88f, 2097.47f, 440.581f, (byte)0));
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 126.64f, 2175.42f, 441.662f, (byte)0));
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 160.65f, 2099.39f, 439.625f, (byte)0));
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 169.32f, 2175.65f, 441.708f, (byte)0));
        SpawnAndObserveNpc(281931, new WorldPosition(210050000, 187.13f, 2137.05f, 441.185f, (byte)0));

        Spawn(282123, 121.00f, 2119.01f, 441.626f, (sbyte)0, 1463);
        Spawn(282123, 142.35f, 2110.25f, 441.213f, (sbyte)0, 1464);
        Spawn(282123, 161.46f, 2119.23f, 441.356f, (sbyte)0, 1757);
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
            case 90:
            case 70:
            case 50:
            case 30:
            case 20:
            case 10:
                SpawnTornado();
                break;
            case 5:
                GetOwner().QueueSkill(18730, 1, 3000); // Berserk State
                break;
        }
    }

    public override bool IsDestinationReached()
    {
        if (GetState() == AIState.FORCED_WALKING)
        {
            if (PositionUtil.IsInRange(GetOwner(), 141.77f, 2142.31f, 440.0f, 4f) && isEggEventActive.CompareAndSet(false, true))
            {
                Npc egg = (Npc)Spawn(281451, 141.77f, 2142.31f, 440.0f, (sbyte)0);
                ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18726, 1, egg).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 2500L);
            }
        }
        return base.IsDestinationReached();
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 18726: // Extract Essence
                isEggEventActive.Set(false);
                SetStateIfNot(AIState.FIGHT);
                GetOwner().SetTarget(GetAggroList().GetTarget(AggroTarget.MOST_HATED));
                GetMoveController().MoveToTargetObject();
                break;
            case 19178: // Deadly Bolt
                List<Creature> targets = GetKnownList().StreamPlayers().Where(p => !p.IsDead() && IsInRange(p, 80)).Cast<Creature>().ToList();
                for (int maxTargetCount = Math.Max(1, (int)(targets.Count * 0.2f)); targets.Count > maxTargetCount;)
                    targets.Remove(Rnd.Get(targets));
                foreach (Creature target in targets)
                    Spawn(281452, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
                break;
            case 19182: // Ear Piercing Shriek
                // ThreadPoolManager.getInstance().schedule(() -> {
                // PacketSendUtility.broadcastPacket(getOwner(), SM_SYSTEM_MESSAGE.STR_MSG_LF4_DRAMATA_LAY_EGG());
                // WalkManager.startForcedWalking(this, 141.77f, 2142.31f, 440.0f);
                // }, 1500);
                break;
        }
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect.GetSkillId() == 19186)
        {
            // shieldRemovalStamp = System.currentTimeMillis();
            DespawnNpcs(282123);
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_LF4_DRAMATA_AWAKENING());
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        // if (System.currentTimeMillis() - shieldRemovalStamp >= TimeUnit.MINUTES.toMillis(5)) {
        // SkillEngine.getInstance().applyEffectDirectly(19186, getOwner(), getOwner());
        // deadThunderShields.set(0);
        // ThreadPoolManager.getInstance().schedule(this::spawnShieldNpcs, Rnd.get(120, 300), TimeUnit.MINUTES);
        // }
        hpPhases.Reset();
        DespawnNpcs(281453, 281451, 281931, 281932, 281933);
    }

    protected override void HandleDied()
    {
        DespawnNpcs(281453, 281451, 281931, 281932, 281933);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        DespawnNpcs(281453, 281451, 281931, 281932, 281933);
        base.HandleDespawned();
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        foreach (Npc npc in GetOwner().GetWorldMapInstance().GetNpcs(npcIds))
            npc.GetController().Delete();
    }

    private void SpawnTornado()
    {
        Creature randomTarget = GetAggroList().GetTarget(AggroTarget.RANDOM, 80);
        if (randomTarget != null)
            Spawn(281453, randomTarget.GetX(), randomTarget.GetY(), randomTarget.GetZ(), (sbyte)0);
    }

    private void HandleObservedNpcDied(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 281931:
                SpawnAndObserveNpc(281932, npc.GetPosition());
                SpawnAndObserveNpc(281932, npc.GetPosition());
                break;
            case 281932:
                SpawnAndObserveNpc(281933, npc.GetPosition());
                SpawnAndObserveNpc(281933, npc.GetPosition());
                break;
            case 281933:
                if (deadThunderShields.IncrementAndGet() >= 24)
                    GetOwner().GetEffectController().RemoveEffect(19186); // Protective Slumber
                break;
        }
    }

    private void SpawnAndObserveNpc(int npcId, WorldPosition pos)
    {
        Npc npc = (Npc)Spawn(npcId, pos.GetX() + Rnd.Get(-5, 5), pos.GetY() + Rnd.Get(-3, 3), pos.GetZ() + 0.4f, (sbyte)0);
        npc.GetObserveController().Attach(new DeathObserver(_ => HandleObservedNpcDied(npc)));
    }
}
