using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tahabata Pyrelord (215280), Dark Poeta. @author Ritsu, Estrayl, with the enrage timer and the
/// death spawn corrected against retail pattern <c>Dragon_G1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two corrections, both well evidenced:
/// <list type="bullet">
/// <item>the enrage ran on a <b>five</b>-minute fuse where retail arms battle timer 9 at
/// <b>ten</b> minutes</item>
/// <item>it started counting on <b>spawn</b>, where retail arms it in <c>on_enter_attack_state</c> —
/// so a group that spent four minutes reaching him arrived with one minute to kill him</item>
/// </list>
/// <para>
/// The primal dragon (281265) he leaves behind on dying was spawned by nothing anywhere.
/// </para>
/// <para>
/// <b>Not translated, and deliberately so.</b> Retail also places two kinds of short-lived marker at
/// fixed arena points — a flame center (281261) on four points shared with Vanuka Infernus, and summon
/// spots (281262, 281263) on four more — each living ten seconds, across most of its timer branches.
/// This class instead spawns faithful subordinates (281258, 281259) off the casts of Eruption of Power
/// and Powerful Flame. Those are not the same things under different ids: retail's are the markers a
/// summon emerges from, ours are the summons. Reconciling the two means rebuilding this fight as a
/// timer table rather than a skill hook, which is more than a correction and is written up rather than
/// attempted here.
/// </para>
/// </remarks>
[AIName("tahabata_pyrelord")]
public class TahabataPyrelordAI : AggressiveNpcAI
{
    /// <summary>Retail's battle timer 9, armed on entering combat.</summary>
    private const long EnrageMillis = 600000L;

    /// <summary>Left where he falls; retail spawns it from <c>on_killed_by_user</c>.</summary>
    private const int PrimalDragon = 281265;

    private ScheduledTask wipeTask;
    private bool engaged;

    public TahabataPyrelordAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (engaged)
            return;

        engaged = true;
        ScheduleWipe();
    }

    private void ScheduleWipe()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_TIME());
        wipeTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
                GetOwner().QueueSkill(19679, 50, 3000);
            return ValueTask.CompletedTask;
        }, EnrageMillis);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 19679: // You are unworthy.
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_END());
                AIActions.DeleteOwner(this);
                break;
            case 18236:
                Spawn(281258, 1191.2714f, 1220.5795f, 144.2901f, (sbyte)36);
                Spawn(281258, 1188.3695f, 1257.1322f, 139.66028f, (sbyte)80);
                Spawn(281258, 1177.1423f, 1253.9136f, 140.58705f, (sbyte)97);
                Spawn(281258, 1163.5889f, 1231.9149f, 145.40042f, (sbyte)118);
                break;
            case 18241:
                Spawn(281259, 1182.0021f, 1244.0125f, 142.67587f, (sbyte)88);
                Spawn(281259, 1192.3885f, 1236.5231f, 142.50638f, (sbyte)68);
                Spawn(281259, 1185.647f, 1227.2747f, 144.2261f, (sbyte)32);
                Spawn(281259, 1172.3302f, 1232.5709f, 144.70761f, (sbyte)12);
                break;
        }
    }

    private void CancelTask()
    {
        if (wipeTask != null && !wipeTask.IsCancelled)
            wipeTask.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTask();
        Spawn(PrimalDragon, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading());
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }
}
