using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatDragonAI (@author Estrayl March 10th, 2018).</summary>
[AIName("tiamat_dragon")]
public class TiamatDragonAI : AggressiveNpcAI
{
    public TiamatDragonAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail <c>IDTiamat_Tiamat_Dragon_Named_60_Al</c>: four drakan mages, one at each corner of the
    /// platform, fifteen seconds after he engages and four more after that.
    /// </summary>
    /// <remarks>
    /// <b>Retail-sourced; see docs/retail-ai-fidelity.md.</b> Java has no equivalent and this class made
    /// no spawns at all, so all four mages sat in <c>npc_templates</c> summoned by nothing.
    /// <para>
    /// Retail builds the delay as two timers: <c>on_enter_attack_state</c> arms timer 1 at fifteen
    /// seconds, its branch arms timer 3 at four more, and the mage branch hangs off timer 3. The pair is
    /// kept because the fifteen-second step is also where retail says its line, and collapsing them to a
    /// single nineteen-second wait would put the line in the wrong place if it is ported later.
    /// </para>
    /// <para>
    /// <b>The rush wave that follows is not built.</b> Retail's other spawn branch sends roughly twenty
    /// drakan along <c>path_tiamatdrakan_*</c> walker paths, and this port has no waypoint support in
    /// either AI layer — the same gap that blocks the silikor dismissal.
    /// </para>
    /// </remarks>
    private static readonly (int NpcId, float X, float Y, float Z, byte Heading)[] Mages =
    [
        (283163, 464.159f, 462.677f, 417.5f, 77),
        (283164, 464.164f, 566.648f, 417.5f, 42),
        (283165, 543.351f, 566.164f, 418.0f, 17),
        (283166, 543.669f, 462.703f, 417.4f, 103),
    ];

    /// <summary>Retail's timer 1 (15s) then timer 3 (4s).</summary>
    private const long MagesDelayMillis = 19000L;

    private readonly AtomicBoolean magesCalled = new AtomicBoolean();

    private ScheduledTask? mageTask;

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);

        // Retail hangs this off on_enter_attack_state behind a test-and-set, so it runs once per fight.
        if (magesCalled.CompareAndSet(false, true))
            mageTask = ThreadPoolManager.GetInstance().Schedule(() => SpawnMages(), MagesDelayMillis);
    }

    /// <summary>
    /// Cancelled on death rather than guarded inside the task.
    /// </summary>
    /// <remarks>
    /// <b>An <c>IsDead()</c> check in the body is not enough</b>: the pin for it failed, because a boss
    /// killed by the death event still reads as alive to that check for as long as the corpse stands.
    /// Cancelling is what the rest of this port does — Stormwing and Pazuzu both cancel their repeating
    /// tasks in <c>HandleDied</c> — and it is the only version that actually stops the summon.
    /// </remarks>
    protected override void HandleDied()
    {
        CancelMageTask();
        Npc owner = GetOwner();
        SpawnFor(ThickDust, owner.GetX(), owner.GetY(), owner.GetZ(),
            (sbyte)owner.GetHeading(), DustLife);
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelMageTask();
        base.HandleBackHome();
    }

    private void CancelMageTask()
    {
        if (mageTask != null && !mageTask.IsCancelled)
            mageTask.Cancel(true);
        mageTask = null;
    }

    private void SpawnMages()
    {
        if (!GetOwner().IsSpawned())
            return;

        foreach ((int npcId, float x, float y, float z, byte heading) in Mages)
            Spawn(npcId, x, y, z, (sbyte)heading);
    }

    /// <summary>
    /// Retail's arrival and death effects, all of which this class was missing.
    /// </summary>
    /// <remarks>
    /// <b>Retail-sourced; see docs/retail-ai-fidelity.md.</b> The hard variant already places every one of
    /// these from its pattern table, using the same npc ids — <b>the two forms differ only in their
    /// drakan, not in their effects</b> — so the normal form arriving in silence was a gap between our two
    /// classes rather than between us and retail generally.
    /// <para>
    /// The flash is at a fixed point on the platform in <c>on_wake_up</c>; the inferno elemental and the
    /// burrowing arrival are at his own feet; the dust is at his own feet on death. All four carry
    /// retail's <c>live_time</c>.
    /// </para>
    /// </remarks>
    private const int ShapeChangeFlash = 283174;
    private const int InfernoSpirit = 283067;
    private const int BurrowingArrival = 283062;
    private const int ThickDust = 283134;

    private const int FlashLife = 10;
    private const int SpiritLife = 6;
    private const int ArrivalLife = 8;
    private const int DustLife = 6;

    /// <summary>Retail's absolute mark for the shape-change flash.</summary>
    private const float FlashX = 457.9f;
    private const float FlashY = 514.5f;
    private const float FlashZ = 417.6f;

    private void SpawnArrivalEffects()
    {
        Npc owner = GetOwner();
        SpawnFor(ShapeChangeFlash, FlashX, FlashY, FlashZ, 0, FlashLife);
        SpawnFor(InfernoSpirit, owner.GetX(), owner.GetY(), owner.GetZ(),
            (sbyte)owner.GetHeading(), SpiritLife);
        SpawnFor(BurrowingArrival, owner.GetX(), owner.GetY(), owner.GetZ(),
            (sbyte)owner.GetHeading(), ArrivalLife);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SpawnArrivalEffects();
        ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill(this, 20920), 4000L);
        ThreadPoolManager.GetInstance().Schedule(() => GetOwner().QueueSkill(20984, 1), 300000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20920:
                AIActions.UseSkill(this, 20975); // Fissure Buff
                AIActions.UseSkill(this, 20976); // Wrath Buff
                AIActions.UseSkill(this, 20977); // Gravity Buff
                AIActions.UseSkill(this, 20978); // Petrification Buff
                break;
            case 20984:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_WARNING_MSG());
                break;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
