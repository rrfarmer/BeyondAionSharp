using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The village chiefs and Advance guards. Retail patterns <c>LDF5_Village_chief01</c>..<c>19</c> and
/// <c>LDF4_Advance_village_01</c>..<c>37</c>.
/// </summary>
/// <remarks>
/// @author Estrayl. Retail-sourced addition below; see docs/retail-ai-fidelity.md and
/// <c>tools/client-extract/audit_npc_call_family.py</c>.
/// <para>
/// <b>These answer the fortress killer, and here they did not.</b> Both families carry the same
/// <c>on_message</c> rung — <c>is_message 30001</c>, then
/// <c>add_hate_point target=OBJI_MESSAGE_SENDER point_to_add=1000000</c> — so a killer broadcasting as
/// it wakes pulls every village guard within fifty metres onto itself. 113 npcs run one of these two
/// patterns and none of them heard it.
/// </para>
/// <para>
/// <b>This is the pairing the mechanic was designed around.</b> The abyss artifact killers share
/// <c>GUARD_DRAGON</c> with a third of the protectors they call, and those correctly ignore them. The
/// Advance killers are <c>LDF4_ADVANCE_DRGUARD</c>, whose tribe lists <c>LDF4_ADVANCE_LGUARD</c> and
/// <c>LDF4_ADVANCE_DGUARD</c> as <c>aggro</c> — the guards this class runs. Hostile by design, in the
/// client's own <c>npc_tribe_relation.xml</c> and in ours.
/// </para>
/// <para>
/// <b>Retail adds no <c>is_enemy</c> guard on this rung</b>, unlike the artifact protectors' version of
/// it, and none is added here. The aggro list applies its own tribe test either way; writing one in
/// would be inventing a condition the pattern does not have.
/// </para>
/// <para>
/// <b>Not translated:</b> the sends. An Advance guard broadcasts <c>30004</c> as it enters combat and
/// then <c>30002</c> every <b>5000</b> off a battle timer — calling a killer to itself for as long as
/// it is fighting — and a village chief broadcasts <c>30002</c> on entering combat and <c>30003</c> on
/// dying, alongside two <c>set_condition_spawn_variable</c> calls that record which race killed it.
/// None of those exist here, so a guard cannot summon its killer; only a killer that wakes on its own
/// is answered.
/// </para>
/// </remarks>
[AIName("base_protector")]
public class BaseProtectorAI : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>Retail's killer-wakes call, and the value both families answer it with.</summary>
    public const int KillerAwake = 30001;
    public const int DropEverything = 1_000_000;

    /// <summary>
    /// Retail's <c>on_message</c> rung: hate on the <b>sender</b>, not on anything it names.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != KillerAwake || IsDead() || sender == GetOwner())
            return;

        SummonOrder.Take(GetOwner(), sender, DropEverything);
    }

    public BaseProtectorAI(Npc owner) : base(owner)
    {
    }

    protected new BaseSpawnTemplate GetSpawnTemplate()
    {
        return (BaseSpawnTemplate)base.GetSpawnTemplate();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        Base @base = BaseService.GetInstance().GetActiveBase(GetSpawnTemplate().GetId());
        if (@base == null)
            return;
        DamageInfo<AionObject> mostDamage = GetAggroList().GetFinalDamageList().ToTeamDamages().GetMostDamage();
        Creature bossKiller = mostDamage.GetAttacker() is TemporaryPlayerTeam team ? team.GetLeaderObject() : (Creature)mostDamage.GetAttacker();
        BaseOccupier newOccupier = @base.GetOccupier(bossKiller);
        BaseService.GetInstance().Capture(@base.GetId(), newOccupier);
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetLevel() >= 65) // Avoid adjusting low-level zones
            stat.SetBaseRate(SiegeConfig.BASE_PROTECTOR_HEALTH_MULTIPLIER);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT or AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE => false,
            _ => base.Ask(question),
        };
    }
}
