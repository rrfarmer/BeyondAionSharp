using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four spider eggs of the Unstable Splinterpath (219564, 219581, 219582, 219583).
/// </summary>
/// <remarks>
/// Retail patterns <c>IDAbRe_Core_Egg_02</c> through <c>_Egg4_02</c>. Each egg hatches one fixed set of
/// spiders; see <see cref="ByEgg"/> for what this class did instead.
/// <para>
/// <b>Not translated: retail's trigger.</b> Its eggs hatch on <c>on_despawn</c>, and they despawn
/// because <c>IDAbRe_Core_NamedB_NPC_02</c> broadcasts <b>111</b> within ten metres. That npc is not
/// spawned by anything in this port, so rewiring the hatch onto that chain would trade a mechanic that
/// works for one that never fires. The existing trigger — Kaluva's debuff, twenty-eight seconds — is
/// kept, and the divergence is recorded rather than swapped for a dead branch.
/// </para>
/// <para>
/// Also not translated: the <c>on_see_npc</c> broadcast that answers a <c>beast</c> walking past, and
/// the marker each egg leaves on dying.
/// </para>
/// </remarks>
[AIName("unstablekaluvaspawn")]
public class UnstableKaluvaSpawnAI : NpcAI
{
    private ScheduledTask task;

    public UnstableKaluvaSpawnAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (task != null && !task.IsDone())
            task.Cancel(true);
        CheckKaluva();
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        OnMoved(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        OnMoved(creature);
    }

    private void OnMoved(Creature creature)
    {
        if (task == null && creature is Npc && ((Npc)creature).GetNpcId() == 219553)
        {
            if (PositionUtil.IsInRange(GetOwner(), creature, 7))
            {
                creature.GetEffectController().RemoveEffect(19152);
                ScheduleHatch();
            }
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19222, 55, GetOwner()).UseNoAnimationSkill();
    }

    private void CheckKaluva()
    {
        Npc kaluva = GetPosition().GetWorldMapInstance().GetNpc(219553);
        if (kaluva != null && !kaluva.IsDead())
        {
            kaluva.GetEffectController().RemoveEffect(19152);
        }
        AIActions.DeleteOwner(this);
    }

    private void ScheduleHatch()
    {
        task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                HatchAdds();
                CheckKaluva();
            }
            return ValueTask.CompletedTask;
        }, 28000L); // schedule hatch when debuff ends(20s)
    }

    /// <summary>Retail's two spiders: <c>bidabre_core_02_Sum_SpiderBig</c> and <c>_SpiderSmall</c>.</summary>
    private const int SpiderBig = 283208;
    private const int SpiderSmall = 283209;

    /// <summary>Retail's <c>spawn_range</c> and <c>live_time</c>, the same on every hatch.</summary>
    private const float Scatter = 5f;
    private const int SpiderLife = 300;

    /// <summary>
    /// What each egg hatches. <b>There are four eggs, not one egg with four formations.</b>
    /// </summary>
    /// <remarks>
    /// This class rolled a die between the four and spawned whichever came up. The four compositions
    /// are recognisably retail's, but each belongs to <em>one</em> egg npc:
    /// <c>IDAbRe_Core_Egg_02</c> hatches twelve small, <c>Egg2_02</c> two big, <c>Egg3_02</c> one big,
    /// and <c>Egg4_02</c> one big and three small. Which egg the raid broke decided nothing.
    /// <para>
    /// <b>And the spiders were the wrong npcs.</b> Ours were 219572, 219573 and 219584 — the
    /// <c>idabre</c>-prefixed family, which <b>no retail pattern spawns anywhere</b>. Retail hatches the
    /// <c>bidabre</c> pair. 219584 in particular is a third species that exists in no hatch at all: its
    /// twin 283227 is placed by nothing, so <c>Egg3_02</c>'s single big spider had been rendered as a
    /// creature of its own.
    /// </para>
    /// <para>
    /// They were also stacked on one point with no lifetime; retail scatters them within five metres
    /// and gives each five minutes. Found by <c>audit_invented_spawns.py</c>.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<int, (int NpcId, int Count)[]> ByEgg =
        new Dictionary<int, (int, int)[]>
        {
            [219564] = [(SpiderSmall, 12)],
            [219581] = [(SpiderBig, 2)],
            [219582] = [(SpiderBig, 1)],
            [219583] = [(SpiderBig, 1), (SpiderSmall, 3)],
        };

    private void HatchAdds()
    {
        if (!ByEgg.TryGetValue(GetNpcId(), out (int NpcId, int Count)[]? hatch))
            return;

        WorldPosition p = GetPosition();
        foreach ((int npcId, int count) in hatch)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = Rnd.NextFloat(360f) * System.Math.PI / 180.0;
                float distance = Rnd.NextFloat(Scatter);
                SpawnFor(npcId,
                    p.GetX() + (float)(System.Math.Cos(angle) * distance),
                    p.GetY() + (float)(System.Math.Sin(angle) * distance),
                    p.GetZ(), (sbyte)p.GetHeading(), SpiderLife);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
