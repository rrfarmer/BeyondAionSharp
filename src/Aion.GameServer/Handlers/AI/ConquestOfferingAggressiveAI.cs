using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using System.Threading.Tasks;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The conquest offering monsters (112 npcs). Retail pattern <c>F4_Rotation_Normal_Monster</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/ConquestOfferingAggressiveAI (Yeats). Retail-sourced corrections below; see
/// docs/retail-ai-fidelity.md.
/// <para>
/// <b>Its death is what closes the rotation.</b> Retail always leaves a <b>time-reset npc</b> (856502)
/// where the monster fell, and about thirty-one times in a hundred a <b>buff npc</b> beside it. That
/// reset npc broadcasts <c>13929</c> every six seconds, which is the message that re-arms the spawner's
/// eight-minute clock — so the loop runs spawner, spot, monster, reset, spawner.
/// </para>
/// <para>
/// <b>This class had the return path wrong at both ends.</b> It rolled 55% and then 45% for a buff npc
/// — the right four ids, at 24.75% rather than retail's 31.4% and uniform rather than retail's
/// descending ladder — and otherwise placed a <c>secret portal</c> (833018/833021), which is not the
/// reset npc and carries no message. <b>Forty-five per cent of deaths produced nothing at all</b>, and
/// the spawner's clock was never reset by anything.
/// </para>
/// <para>
/// Retail's ladder is four branches at nine per cent each, first match wins, so the four buff npcs are
/// not equally likely: 9, 8.19, 7.45 and 6.78 per cent.
/// </para>
/// <para>
/// <b>And it has a combat mechanic, which this class had none of.</b> On entering combat retail arms a
/// six-second battle timer, and every turn of it is a fifteen per cent roll to drop
/// <c>BF4_Rotation_Skill_NPC</c> (856297) on whoever it is fighting, within fifty metres and with no
/// hate. Nothing here did anything between spawning and dying.
/// </para>
/// <para>
/// <b>Not translated.</b> Both self-casts — retail's <c>on_wake_up</c> here and the skill npc's own —
/// name <c>SKILLI_INDEX_0</c>, and neither npc has a row in our npc skill data, so there is no skill to
/// cast. The skill npc is placed and behaves as retail's does in every other respect; what it would
/// have cast is missing from the data rather than from this class.
/// </para>
/// </remarks>
[AIName("conquest_offering_aggressive")]
public class ConquestOfferingAggressiveAI : AggressiveNpcAI
{
    private Npc spawner;

    public ConquestOfferingAggressiveAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary><c>BF4_Rotation_Skill_NPC</c>, dropped on the current target.</summary>
    private const int SkillNpc = 856297;

    /// <summary>Retail's <c>valid_distance</c> on that drop, and its <c>test_probability</c>.</summary>
    private const float SkillNpcReach = 50f;
    private const int SkillNpcChance = 15;

    /// <summary>Retail's <c>BTIMERI_INDEX_0</c>, armed on entering combat and re-armed every turn.</summary>
    private const long BattleTimerMillis = 6_000L;

    private ScheduledTask? battleTimer;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        FindAndSetCreator();
    }

    /// <summary>Retail's <c>on_enter_attack_state</c>: arm the six-second turn, once.</summary>
    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);

        if (battleTimer == null)
            ArmBattleTimer();
    }

    private void ArmBattleTimer()
    {
        battleTimer = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            BattleTurn();
            return ValueTask.CompletedTask;
        }, BattleTimerMillis);
    }

    /// <summary>
    /// One turn of retail's battle timer: re-arm on every rung, and drop the skill npc on fifteen.
    /// </summary>
    private void BattleTurn()
    {
        if (!GetOwner().IsSpawned() || GetOwner().IsDead())
            return;

        // Both of retail's rungs re-arm, so the chain runs for as long as the fight does.
        ArmBattleTimer();

        if (Rnd.NextInt(100) >= SkillNpcChance)
            return;

        // On the current target and nowhere else, so a monster with nothing in front of it drops
        // nothing -- retail's spawn_on_target with no target simply does not fire.
        if (GetOwner().GetTarget() is not Creature target)
            return;

        // Retail's valid_distance: fifty metres, measured from the monster.
        if (!PositionUtil.IsInRange(GetOwner(), target, SkillNpcReach))
            return;

        Spawn(SkillNpc, target.GetX(), target.GetY(), target.GetZ(), (sbyte)target.GetHeading());
    }

    private void StopBattleTimer()
    {
        if (battleTimer != null && !battleTimer.IsDone())
            battleTimer.Cancel(true);
        battleTimer = null;
    }

    protected override void HandleDespawned()
    {
        StopBattleTimer();
        base.HandleDespawned();
    }

    private void FindAndSetCreator()
    {
        if (GetCreatorId() != 0 && GetPosition().GetWorldMapInstance().GetObject(GetCreatorId()) is Npc npc)
            spawner = npc;
    }

    protected override void HandleDied()
    {
        StopBattleTimer();

        // The notification is conditional and the placement is not. Retail's death branches carry no
        // condition at all -- a monster whose spawner has gone still leaves its reset npc, and this
        // class placed nothing in that case, which is also every case a pin can construct.
        if (spawner != null && !spawner.IsDead())
            spawner.GetAi().OnCustomEvent(1);

        SpawnRandomNpc();
        base.HandleDied();
    }

    /// <summary><c>BF4_Rotation_Time_Reset_BR_NPC</c>, which carries the message home.</summary>
    private const int TimeResetNpc = 856502;

    /// <summary><c>BF4_Rotation_Buff_NPC_01</c> through <c>_04</c>.</summary>
    private static readonly int[] BuffNpcs = [856175, 856176, 856177, 856178];

    /// <summary>Retail's <c>test_probability</c> on each rung of the buff ladder.</summary>
    private const int BuffChance = 9;

    /// <summary>
    /// Retail's death ladder: the reset npc always, and a buff npc on a descending four-rung roll.
    /// </summary>
    private void SpawnRandomNpc()
    {
        // First match wins, so each rung is only reached if the ones above it failed -- which is what
        // makes the four buff npcs 9, 8.19, 7.45 and 6.78 per cent rather than a flat share.
        foreach (int buff in BuffNpcs)
        {
            if (Rnd.NextInt(100) < BuffChance)
            {
                Place(buff);
                break;
            }
        }

        // And the reset npc on every rung, including the one that carries no buff at all.
        Place(TimeResetNpc);
    }

    private void Place(int npcId)
    {
        Spawn(npcId, GetOwner().GetX() + 0.3f, GetOwner().GetY() + 0.3f, GetOwner().GetZ() + 0.2f,
            (sbyte)GetOwner().GetHeading());
    }
}
