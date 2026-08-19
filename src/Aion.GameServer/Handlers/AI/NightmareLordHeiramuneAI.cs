using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Nightmare Lord Heiramune. Retail pattern <c>IDAsteria_IU_world_3Stage_Boss</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/nightmareCircus/NightmareLordHeiramuneAI (@author Ritsu). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The endless add train at eighty per cent was invented.</b> This class started a fixed-rate task
/// there that put <b>two enraged nightmares on the floor every twenty seconds for the rest of the
/// fight</b>, at hardcoded coordinates. Retail's eighty-per-cent rung is two lines — a shout and
/// <c>set_condition_spawn_variable Condition_S3 modify=1</c> — and <b>nothing anywhere in the pattern
/// spawns on a repeating timer at all</b>. Its three <c>on_battle_timer</c> rungs are casts.
/// </para>
/// <para>
/// <b>And the npc it used belongs to a different wave.</b> 233457 is
/// <c>IDAsteria_IU_WORLD_2w_Mammoth_65_Ae</c> — a <b>second</b>-stage event npc. The third-stage boss
/// never spawns it in retail. Its 55% add, 233162, is
/// <c>IDAsteria_IU_3w_Shu_Fi_65_An</c>, which is right and is kept.
/// </para>
/// <para>
/// <b>The fortieth per cent was missing.</b> Retail shouts at 80, 55 and 40; this class had only the
/// first two. Both 80 and 40 carry <c>STR_CHAT_..._Gossip_15</c> and 55 carries <c>Gossip_14</c>, which
/// is how the two message ids already here are matched up: 1501139 to the pair, 1501138 to the add.
/// That mapping is <b>inferred from the pairing, not resolved</b> — the shout ids are not translatable.
/// </para>
/// <para>
/// <b>Not translated.</b> <c>Condition_S3</c> is a conditional-spawn variable: retail bumps it at 80 and
/// again at 40, and whatever the world spawn tables hang off it is what arrives. This port has no
/// equivalent, so those two thresholds now shout and nothing else — which is closer to retail than a
/// twenty-second train, but is not the whole rung. His three battle-timer casts (a different skill in
/// each of the 100-71, 70-31 and lower bands, re-armed at 8000) are skill indices and remain absent,
/// as does the pair he casts on entering attack state.
/// </para>
/// </remarks>
[AIName("nightmarelordheiramune")]
public class NightmareLordHeiramuneAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>Retail's three <c>on_attacked</c> thresholds, in the order he crosses them.</summary>
    private readonly HpPhases hpPhases = new HpPhases(80, 55, 40);

    /// <summary><c>IDAsteria_IU_3w_Shu_Fi_65_An</c>, his one add.</summary>
    private const int Add = 233162;

    /// <summary>
    /// <c>IDAsteria_IU_WORLD_2w_Mammoth_65_Ae</c> — a second-wave npc this boss never spawns in retail.
    /// Kept only so that a floor left dirty by an older build is still cleared when he dies.
    /// </summary>
    private const int SecondWaveMammoth = 233457;

    /// <summary>Retail's <c>Gossip_14</c> on the add rung, and <c>Gossip_15</c> on the other two.</summary>
    private const int AddShout = 1501138;
    private const int ThresholdShout = 1501139;

    public NightmareLordHeiramuneAI(Npc owner) : base(owner)
    {
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
            case 40:
                // Retail: say_to_all, then set_condition_spawn_variable Condition_S3 modify=1.
                // The condition variable has no equivalent here; see the class remarks.
                PacketSendUtility.BroadcastMessage(GetOwner(), ThresholdShout);
                break;
            case 55:
                PacketSendUtility.BroadcastMessage(GetOwner(), AddShout);
                Spawn(Add, GetOwner().GetX() + 5, GetOwner().GetY() + 5, GetOwner().GetZ(), (sbyte)GetOwner().GetHeading());
                break;
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DespawnNpcs(SecondWaveMammoth, Add);
    }

    /// <summary>
    /// Retail's add carries <c>despawn_at_attack_state=TRUE</c>, so it goes when he leaves the fight.
    /// </summary>
    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        DespawnNpcs(SecondWaveMammoth, Add);
        hpPhases.Reset();
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(npcIds))
        {
            npc.GetController().Delete();
        }
    }
}
