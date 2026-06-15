using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("drakenspire_seal_guardian")]
public class SealGuardianAI : AggressiveNoLootNpcAI
{
    private readonly AtomicBoolean isIdling = new AtomicBoolean(true);
    private ScheduledTask idleTimer;

    public SealGuardianAI(Npc owner) : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_WIND;
    }

    private Player GetLastAttacker()
    {
        AggroInfo lastAttacker = GetAggroList().Stream()
            .Where(ai => ai.GetAttacker() is Player player && !player.IsDead())
            .OrderByDescending(ai => ai.GetLastInteractionTime()).FirstOrDefault();
        return lastAttacker != null ? (Player)lastAttacker.GetAttacker() : null;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21882 && skillLevel == 57)
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_SYSTEM_MESSAGE(ChatType.NPC, GetOwner(), 1501357)); // Intruder…
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21882)
            GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 10000);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetOwner().GetGameStats().SetNextSkillDelay(0);
        StartIdleTimer();
        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnSpecter(); return ValueTask.CompletedTask; }, 1000L);
    }

    private void StartIdleTimer()
    {
        idleTimer = ThreadPoolManager.GetInstance().Schedule(_ => { Despawn(); return ValueTask.CompletedTask; }, 60000L);
    }

    private void Despawn()
    {
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_SYSTEM_MESSAGE(ChatType.NPC, GetOwner(), 1501358)); // Teleport…
        NotifyBeritra(2);
        AIActions.DeleteOwner(this);
    }

    private void SpawnSpecter()
    {
        switch (GetNpcId())
        {
            case 855460:
                Spawn(855452, 141.618f, 498.609f, 1749.590f, (sbyte)30);
                break;
            case 855461:
                Spawn(855454, 172.045f, 509.876f, 1749.590f, (sbyte)45);
                break;
            case 855462:
                Spawn(855456, 172.142f, 525.665f, 1749.590f, (sbyte)75);
                break;
            case 855463:
                Spawn(855458, 142.027f, 536.810f, 1749.590f, (sbyte)90);
                break;
            // Will only spawn during dragon phase
            case 855464:
            case 855465:
            case 855466:
            case 855467:
            case 855468:
            case 855469:
                Spawn(855452, 141.618f, 498.609f, 1749.590f, (sbyte)30);
                Spawn(855454, 172.045f, 509.876f, 1749.590f, (sbyte)45);
                Spawn(855456, 172.142f, 525.665f, 1749.590f, (sbyte)75);
                Spawn(855458, 142.027f, 536.810f, 1749.590f, (sbyte)90);
                break;
        }
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isIdling.CompareAndSet(true, false))
            CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        Despawn();
    }

    protected override void HandleDied()
    {
        Player lastAttacker = GetLastAttacker();
        if (lastAttacker != null)
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21625, GetOwner(), lastAttacker);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_SYSTEM_MESSAGE(ChatType.NPC, GetOwner(), 1501359)); // I shall… curse you…

        NotifyBeritra(1);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        DespawnSpecters();
        base.HandleDespawned();
    }

    private void CancelTask()
    {
        if (idleTimer != null && !idleTimer.IsDone())
            idleTimer.Cancel(true);
    }

    private void DespawnSpecters()
    {
        int[] specterIds = GetNpcId() switch
        {
            855460 => new int[] { 855452 },
            855461 => new int[] { 855454 },
            855462 => new int[] { 855456 },
            855463 => new int[] { 855458 },
            _ => new int[] { 855452, 855454, 855456, 855458 },
        };

        GetPosition().GetWorldMapInstance().GetNpcs(specterIds).ForEach(npc => npc.GetController().DeleteIfAliveOrCancelRespawn());
    }

    private void NotifyBeritra(int eventId)
    {
        List<Npc> possibleBeritras = GetPosition().GetWorldMapInstance().GetNpcs(236244, 236245, 236246);
        if (possibleBeritras.Count != 0)
        {
            possibleBeritras[0].GetAi().OnCustomEvent(eventId); // 1 = Death, 2 = back home | 60s idle
        }
    }
}
