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
/// The Drakenspire seal guardian chiefs (855460-855469). Retail patterns
/// <c>IDSeal_Guardian_Chief_01</c> through <c>_04</c> and <c>_11</c> through <c>_16</c>.
/// </summary>
/// <remarks>
/// Java parity: @author Estrayl. Retail-sourced additions below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>All ten chiefs carry the same two markers, and neither was placed.</b> On waking each drops a
/// <b>delay keeper</b> at its own feet for eighty seconds, and on dying it leaves a <b>reset marker</b>
/// for ten. Both npcs were already in our data with an AI of their own; nothing summoned them.
/// </para>
/// <para>
/// <b>One marker on death, not two.</b> Retail writes the branch twice — <c>on_killed_by_user</c> and
/// <c>on_killed_by_npc</c>, byte-identical — and both carry the same test-and-set flag var, so the first
/// match sets it and the second can never run. Fourth time this log has recorded that idiom.
/// </para>
/// <para>
/// <b>The specter placement is kept although retail does not have it.</b> No pattern in the 5.8 dump
/// places 855452/855454/855456/855458 and they are in no spawn file, so this class is the only thing
/// that puts a guardian in that room; retail evidently places them from instance data the pattern dump
/// does not cover. Removing it would trade a mechanic that works for nothing at all.
/// </para>
/// <para>
/// <b>Its dying broadcast has a listener after all.</b> An earlier pass recorded <c>22610</c> as having
/// none. It is the <b>delay keeper</b> the chief itself drops on waking: it hears the broadcast at fifty
/// metres, casts on the named killer, and leaves twenty seconds later instead of standing out the rest
/// of its eighty. 855540 was bound to <c>general</c>, so a dead chief left its keeper standing.
/// <para>
/// <b>Not translated.</b> The two condition variables each chief sets — <c>GUARDIAN_1</c> on waking and
/// <c>GUARDIAN_TIMER</c> on dying — and the keeper's own cast, which names <c>SKILLI_INDEX_0</c> against
/// the killer and has no row in our npc skill data.
/// </para>
/// </para>
/// </remarks>
[AIName("drakenspire_seal_guardian")]
public class SealGuardianAI : AggressiveNoLootNpcAI
{
    /// <summary><c>BIDSeal_Skill_Delay_Keep</c>, dropped on waking and standing eighty seconds.</summary>
    private const int DelayKeeper = 855540;
    private const int DelayKeeperLife = 80;

    /// <summary>Retail's <c>range_as_meter</c> on the chief's dying broadcast.</summary>
    private const float KillerEarshot = 50f;

    /// <summary><c>BIDSeal_Guardian_Chief_Reset_01</c>, left where the chief fell for ten seconds.</summary>
    private const int ResetMarker = 855538;
    private const int ResetMarkerLife = 10;

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

        // Retail's on_wake_up, which this class did not have. Eighty seconds is a fifth longer than the
        // minute the chief waits before teleporting out, so the keeper outlives an untouched chief.
        SpawnFor(DelayKeeper, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading(), DelayKeeperLife);
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

        // Retail's death branch: one marker where he fell, for ten seconds. Placed before base, which
        // clears his position -- retail's branch runs while he is still standing there.
        SpawnFor(ResetMarker, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading(), ResetMarkerLife);

        // And the broadcast that sends his delay keeper away, carrying the killer as retail does.
        NpcMessageBus.Broadcast(GetOwner(), SealDelayKeeperAI.ChiefKilled, lastAttacker, KillerEarshot, null);
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
