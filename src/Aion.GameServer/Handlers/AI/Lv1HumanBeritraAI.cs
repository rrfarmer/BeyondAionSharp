using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/Lv1HumanBeritraAI (Estrayl).</summary>
[AIName("drakenspire_lv1_human_beritra")]
public class Lv1HumanBeritraAI : AggressiveNoLootNpcAI
{
    private static readonly ILogger log = NullLogger.Instance;
    protected readonly AtomicBoolean isActivated = new AtomicBoolean();
    private ScheduledTask spawnTask;
    private long fightStartTime;

    public Lv1HumanBeritraAI(Npc owner)
        : base(owner)
    {
    }

    protected virtual void HandleFightStarted()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (isActivated.Get()) // just in case he resets immediately
                SpawnFactionHelpers(GetAttackingPlayerRace() == Race.ELYOS ? new List<int> { 209736, 209737, 209737 } : new List<int> { 209801, 209802, 209802 });
            return ValueTask.CompletedTask;
        }, 10000L);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isActivated.CompareAndSet(false, true))
        {
            fightStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ApplyBuffs();
            ScheduleNewSealGuardianSpawn();
            HandleFightStarted(); // Only relevant for Lv1 Beritra
        }
    }

    private void ApplyBuffs()
    {
        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21612, GetOwner(), GetOwner());
        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21611, GetOwner(), GetOwner());
        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21610, GetOwner(), GetOwner());
    }

    private void ScheduleNewSealGuardianSpawn()
    {
        if (isActivated.Get())
            spawnTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnSealGuardian(); return ValueTask.CompletedTask; }, 60000L);
    }

    /// <summary>Retail sequence</summary>
    private void SpawnSealGuardian()
    {
        if (Rnd.Chance() < 25)
        {
            Spawn(855460, 128.621f, 461.719f, 1754.576f, (sbyte)15);
        }
        else if (Rnd.Chance() < 33)
        {
            Spawn(855461, 207.780f, 496.081f, 1754.524f, (sbyte)40);
        }
        else if (Rnd.Chance() < 50)
        {
            Spawn(855462, 208.671f, 542.410f, 1754.609f, (sbyte)67);
        }
        else
        {
            Spawn(855463, 127.028f, 574.691f, 1754.681f, (sbyte)103);
        }
    }

    protected override void HandleCustomEvent(int eventId, params object[] args)
    {
        switch (eventId)
        {
            case 1:
                ScheduleNewSealGuardianSpawn();
                break;
            case 2:
                SpawnSealGuardian();
                break;
        }
    }

    public override void OnEffectApplied(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 20834:
            case 20835:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501272); // You insects think you have a chance against me?
                HandleBuffsRemovedByNpc();
                break;
        }
    }

    /// <summary>Retail sequence => Beritra will immediately execute 21604 + 21603 (Rending Shadow);</summary>
    protected void HandleBuffsRemovedByNpc()
    {
        int chainId = GetOwner().GetGameStats().GetLastSkill().GetNextChainId();
        NpcSkillEntry entry = GetOwner().GetSkillList().GetNpcSkills().FirstOrDefault(nse => nse.GetChainId() == chainId);

        GetOwner().QueueSkill(21604, 56, 0);
        GetOwner().QueueSkill(21603, 56, entry == null ? -1 : 0);
        if (entry != null)
            GetOwner().QueueSkill(entry);
    }

    /// <summary>
    /// Retail: Use one of the following patterns:
    /// 25% => 1 3 6; 33% => 2 5 7; 50% => 3 5 8; Fix => 1 4 7
    /// </summary>
    private void HandlePulseWave()
    {
        // 855541
        Npc skill1 = (Npc)Spawn(855742, 151.9f, 518.6f, 1749.6f, (sbyte)0);
        Npc skill2 = (Npc)Spawn(855742, 151.9f, 518.6f, 1749.6f, (sbyte)0);
        Npc skill3 = (Npc)Spawn(855742, 151.9f, 518.6f, 1749.6f, (sbyte)0);

        Npc slave1, slave2, slave3;
        if (Rnd.Chance() < 25)
        {
            slave1 = (Npc)Spawn(855745, 136.6f, 496.7f, 1749.9f, (sbyte)0); // Pos 1
            slave2 = (Npc)Spawn(855747, 137.5f, 534.4f, 1749.9f, (sbyte)0); // Pos 3
            slave3 = (Npc)Spawn(855750, 180.9f, 515.4f, 1749.9f, (sbyte)0); // Pos 6
        }
        else if (Rnd.Chance() < 33)
        {
            slave1 = (Npc)Spawn(855746, 129.6f, 516.2f, 1749.9f, (sbyte)0); // Pos 2
            slave2 = (Npc)Spawn(855749, 173.7f, 534.0f, 1749.9f, (sbyte)0); // Pos 5
            slave3 = (Npc)Spawn(855751, 174.3f, 498.2f, 1749.9f, (sbyte)0); // Pos 7
        }
        else if (Rnd.Chance() < 50)
        {
            slave1 = (Npc)Spawn(855747, 137.5f, 534.4f, 1749.9f, (sbyte)0); // Pos 3
            slave2 = (Npc)Spawn(855749, 173.7f, 534.0f, 1749.9f, (sbyte)0); // Pos 5
            slave3 = (Npc)Spawn(855752, 156.4f, 490.2f, 1749.9f, (sbyte)0); // Pos 8
        }
        else
        {
            slave1 = (Npc)Spawn(855745, 136.6f, 496.7f, 1749.9f, (sbyte)0); // Pos 1
            slave2 = (Npc)Spawn(855748, 154.6f, 540.7f, 1749.9f, (sbyte)0); // Pos 4
            slave3 = (Npc)Spawn(855751, 174.3f, 498.2f, 1749.9f, (sbyte)0); // Pos 7
        }

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            int skillId = GetPulseWaveSkillId();
            SkillEngine.SkillEngine.GetInstance().GetSkill(skill1, skillId, 1, slave1).UseNoAnimationSkill();
            SkillEngine.SkillEngine.GetInstance().GetSkill(skill2, skillId, 1, slave2).UseNoAnimationSkill();
            SkillEngine.SkillEngine.GetInstance().GetSkill(skill3, skillId, 1, slave3).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 500L);
        ThreadPoolManager.GetInstance().Schedule(_ => { DespawnNpcs(skill1, skill2, skill3, slave1, slave2, slave3); return ValueTask.CompletedTask; }, 8000L);
    }

    private int GetPulseWaveSkillId()
    {
        return GetNpcId() == 236247 ? 21623 : 21828;
    }

    /// <summary>Spawn soul extinction fields on three random players within 50m.</summary>
    private void HandleSoulExtinctionFields()
    {
        List<Creature> playersInRange = GetAggroList().StreamValidTargets(50).Where(c => c is Player).ToList();
        Shuffle(playersInRange);
        foreach (Creature p in playersInRange.Take(3))
            Spawn(855450, p.GetX(), p.GetY(), p.GetZ(), (sbyte)0);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rnd.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Retail: NPCs are spawned solely to display effects. To keep it simple, we just calculate the effects within the NPC's AI.
    /// </summary>
    private void HandleDimensionalWave()
    {
        // equals area_id="10"
        ThreadPoolManager.GetInstance().Schedule(_ => { Spawn(855435, 151.991f, 518.583f, 1749.5945f, (sbyte)90); return ValueTask.CompletedTask; }, 3000L);
        // equals area_id="20"
        ThreadPoolManager.GetInstance().Schedule(_ => { Spawn(855435, 152.002f, 518.548f, 1749.5945f, (sbyte)30); return ValueTask.CompletedTask; }, 7500L);
        // equals area_id="30"
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(856300, 126.977f, 574.736f, 1754.6809f, (sbyte)0);
            Spawn(856300, 208.552f, 542.472f, 1754.6082f, (sbyte)0);
            Spawn(856300, 177.031f, 458.650f, 1759.8838f, (sbyte)0);
            Spawn(856300, 176.057f, 579.624f, 1760.0452f, (sbyte)0);
            Spawn(856300, 207.733f, 496.071f, 1754.5236f, (sbyte)0);
            Spawn(856300, 128.722f, 461.584f, 1754.5775f, (sbyte)0);
            return ValueTask.CompletedTask;
        }, 12000L);
    }

    public override void OnStartUseSkill(SkillTemplate st, int level)
    {
        switch (st.GetSkillId())
        {
            case 21601: // Pulse Wave
                HandlePulseWave();
                AddHateToRandomTarget();
                break;
            case 21602: // Dimensional Wave
                HandleDimensionalWave();
                break;
        }
    }

    private void AddHateToRandomTarget()
    {
        GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 100000);
    }

    public override void OnEndUseSkill(SkillTemplate st, int level)
    {
        switch (st.GetSkillId())
        {
            case 21609: // Soul Extinction Field
                switch (level)
                {
                    case 57:
                        PacketSendUtility.BroadcastMessage(GetOwner(), 1501269); // You're not too bad for an insect! | Could also be 1501274
                        break;
                    case 58:
                        PacketSendUtility.BroadcastMessage(GetOwner(), 1501270); // I'm not playing anymore! | Could also be 1501275
                        break;
                }
                HandleSoulExtinctionFields();
                break;
        }
    }

    /// <summary>
    /// Deviation to retail implementation: Guards should spawn with a distance of 8m relative to Beritra's current
    /// position. To avoid spawning them outside the arena, we will spawn them relative to the center of the arena.
    /// </summary>
    protected void SpawnFactionHelpers(List<int> npcIds)
    {
        SpawnTemplate st = GetSpawnTemplate();
        foreach (int npcId in npcIds)
        {
            float distance = Rnd.NextFloat() * 5;
            double angleRadians = (Rnd.NextFloat(360f)) * Math.PI / 180.0;
            float x = st.GetX() + (float)(Math.Cos(angleRadians) * distance);
            float y = st.GetY() + (float)(Math.Sin(angleRadians) * distance);
            Npc helper = (Npc)Spawn(npcId, x, y, st.GetZ(), (sbyte)0);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                helper.SetTarget(GetOwner());
                helper.GetAggroList().AddHate(GetOwner(), 100000);
                helper.GetMoveController().MoveToTargetObject();
                return ValueTask.CompletedTask;
            }, 500L);
        }
    }

    private void DespawnNpcs(params Npc[] npcs)
    {
        foreach (Npc npc in npcs)
            if (npc != null)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    private void DespawnNpcsById(params int[] npcIds)
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(npcIds))
            npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    private bool IsTargetInsideArena()
    {
        SpawnTemplate st = GetSpawnTemplate();
        return PositionUtil.IsInRange(GetTarget(), st.GetX(), st.GetY(), st.GetZ(), 28);
    }

    protected override void HandleTargetTooFar()
    {
        if (!IsTargetInsideArena())
        {
            GetAggroList().StopHating(GetTarget());
            Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (mostHated != null)
                OnCreatureEvent(AiEventType.TARGET_CHANGED, mostHated);
            else
                OnGeneralEvent(AiEventType.TARGET_GIVEUP);
            return;
        }
        base.HandleTargetTooFar();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        DespawnNpcsById(209734, 209735, 209736, 209737, 209799, 209800, 209801, 209802, 855446, 855460, 855461, 855462, 855463);
        CancelTasks();
        fightStartTime = 0;
        isActivated.Set(false);
    }

    protected override void HandleDied()
    {
        CancelTasks();
        LogMetrics();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        isActivated.Set(false);
        DespawnNpcsById(209734, 209735, 209736, 209737, 209799, 209800, 209801, 209802, 855446, 855460, 855461, 855462, 855463);
        base.HandleDespawned();
    }

    protected Race GetAttackingPlayerRace()
    {
        Player p = GetKnownList().StreamPlayers().FirstOrDefault(pl => !pl.IsStaff());
        return p != null ? p.GetRace() : Race.ELYOS;
    }

    private void CancelTasks()
    {
        if (spawnTask != null && !spawnTask.IsDone())
            spawnTask.Cancel(true);
    }

    private void LogMetrics()
    {
        long fullFightTime = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - fightStartTime) / 1000;
        string damageDealt = string.Join(", ", GetAggroList().GetFinalDamageList().GetCreatureDamages()
            .OrderByDescending(di => di.GetDamage())
            .Select(ai => string.Format("{0} (ID: {1}, Dmg: {2})", ai.GetAttacker().GetName(), ai.GetAttacker().GetObjectId(), ai.GetDamage())));

        log.LogInformation("[{0}] {1} (ID:{2}) was killed in {3}s. Damage List: {4}", GetPosition().GetWorldMapInstance().GetTemplate().GetName(), GetOwner().GetName(),
            GetNpcId(), fullFightTime, damageDealt);
    }
}
