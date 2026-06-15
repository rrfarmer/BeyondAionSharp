using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/gelkmaros/PadmarashkaAI (Estrayl).</summary>
[AIName("padmarashka_world_boss")]
public class GelkmarosPadmarashkaAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(33, 5);
    private readonly AtomicInteger deadProtectors = new AtomicInteger();

    public GelkmarosPadmarashkaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19186, GetOwner(), GetOwner());
            return ValueTask.CompletedTask;
        }, 3000L);
        SpawnShieldNpcs();
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        switch (stat.GetStat())
        { // Tweak for 12p (600 s | 3000 dps)
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
        SpawnAndObserveNpc(281938, 2906.05f, 865.15f, 35.289f, (byte)107);
        SpawnAndObserveNpc(281939, 2920.70f, 878.94f, 35.289f, (byte)94);
        SpawnAndObserveNpc(281940, 2952.03f, 878.61f, 35.266f, (byte)81);
        SpawnAndObserveNpc(281941, 2963.97f, 859.07f, 35.289f, (byte)69);
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
            case 33:
                SpawnRockSlides();
                break;
            case 5:
                GetOwner().QueueSkill(18730, 1, 3000); // Berserk State
                break;
        }
    }

    private void SpawnRockSlides()
    {
        for (int i = 0; i < 360; i += 9)
        {
            float distance = Rnd.Get(400, 700) * 0.1f;
            double radian = Math.PI / 180.0 * i;
            float x = (float)(Math.Cos(radian) * distance);
            float y = (float)(Math.Sin(radian) * distance);
            Spawn(281936, 2940.20f + x, 851.29f + y, 35.89f, (sbyte)0);
        }
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect.GetSkillId() == 19186)
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_DF4_DRAMATA_AWAKENING());
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        DespawnNpcs(281936);
    }

    protected override void HandleDespawned()
    {
        DespawnNpcs(281938, 281939, 281940, 281941, 281936);
        base.HandleDespawned();
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        GetOwner().GetWorldMapInstance().GetNpcs(npcIds).ForEach(npc => npc.GetController().Delete());
    }

    private void HandleObservedNpcDied(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 281938:
            case 281939:
            case 281940:
            case 281941:
                if (deadProtectors.IncrementAndGet() >= 4)
                    GetOwner().GetEffectController().RemoveEffect(19186); // Protective Slumber
                break;
        }
    }

    private void SpawnAndObserveNpc(int npcId, float x, float y, float z, byte h)
    {
        Npc npc = (Npc)Spawn(npcId, x, y, z, (sbyte)h);
        npc.GetObserveController().Attach(new DeathObserver(_ => HandleObservedNpcDied(npc)));
    }
}
