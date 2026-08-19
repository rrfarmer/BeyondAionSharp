using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Padmarashka's eggs (282613, 282614). Retail patterns <c>IDDramata_Egg_01</c> and
/// <c>IDDramata_H_Egg_01</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/padmarashkasCave/PadmarashkaEggAI (@author Ritsu). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Both hatch timers carried a <c>TODO: Need right value</c>, and retail states both.</b> Each egg
/// sets <c>set_idle_timer delay=60000</c> on waking and despawns itself when it turns — so <b>the huge
/// egg hatches at sixty seconds, not the hundred and twenty this port guessed</b>. Twice as long is
/// twice as much time to kill it, which is the whole of that mechanic.
/// </para>
/// <para>
/// <b>Killing an egg stops it hatching</b>, and retail says so structurally rather than by a check: the
/// hatch lives in <c>on_despawn</c> and both it and <c>on_die</c> share one test-and-set flag var, so
/// whichever fires first locks the other out. This class leaned on reading <c>IsDead()</c> when the
/// timer turned, <b>which is a weaker thing</b> — it holds only if the life stats have already been
/// written by the time the hatch runs. The death now cancels the hatch outright, which is what retail's
/// shared flag does; the <c>IsDead()</c> guard is kept behind it.
/// </para>
/// <para>
/// <b>A dying egg buffs every nearby protector, not just its own.</b> Retail broadcasts message 105 at
/// <b>fifty metres</b> and each hatcher in earshot answers by buffing itself. This class buffed only the
/// protector that egg had spawned, and only if it had spawned one — so an egg killed before it was ever
/// attacked buffed nothing, and a second hatcher standing beside it was missed.
/// </para>
/// <para>
/// <b>Not translated.</b> The huge egg also broadcasts 106 at thirty metres as it hatches, and the
/// hatchers answer it with <c>goto_alias</c> — they reposition to a named point. This port has no alias
/// table, so the hero drakan arrives without the escort shuffling to meet it.
/// </para>
/// </remarks>
[AIName("padmarashkaegg")]
public class PadmarashkaEggAI : NpcAI
{
    /// <summary>Retail's <c>set_idle_timer</c> on both eggs, which is where the hatch comes from.</summary>
    private const long HatchMillis = 60_000L;

    private ScheduledTask hatchTask;

    bool isSmallEggProtectorSpawned = false;
    bool isHugeEggProtectorSpawned = false;
    private Npc protector = null;

    public PadmarashkaEggAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary><c>IDDramata_SumDrakanFiEgg</c>, <c>_WiEgg</c> and the elite commander.</summary>
    private static readonly int[] Protectors = [282715, 282716, 282712];

    /// <summary>Retail's <c>range_as_meter</c> on the egg's dying broadcast.</summary>
    private const float Earshot = 50f;

    /// <summary>Retail's message 105: every hatcher in earshot buffs itself.</summary>
    protected override void HandleDied()
    {
        // Retail's flag var: whichever of die and despawn comes first locks the other out.
        if (hatchTask != null && !hatchTask.IsDone())
            hatchTask.Cancel(true);
        hatchTask = null;

        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs())
        {
            if (npc == null || npc.IsDead() || !Protectors.Contains(npc.GetNpcId()))
                continue;
            if (!PositionUtil.IsInRange(GetOwner(), npc, Earshot))
                continue;

            SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 20176, 55, npc).UseNoAnimationSkill();
        }

        base.HandleDied();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (!isSmallEggProtectorSpawned && this.GetNpcId() == 282613)
        {
            switch (Rnd.Get(1, 6))
            {
                case 1:
                    protector = (Npc)Spawn(282715, 579.415f, 168.109f, 66.000f, (sbyte)0);
                    break;
                case 2:
                    protector = (Npc)Spawn(282715, 581.316f, 157.520f, 66.000f, (sbyte)0);
                    break;
                case 3:
                    protector = (Npc)Spawn(282715, 575.073f, 147.338f, 66.000f, (sbyte)0);
                    break;
                case 4:
                    protector = (Npc)Spawn(282715, 585.119f, 150.989f, 66.000f, (sbyte)0);
                    break;
                case 5:
                    protector = (Npc)Spawn(282716, 581.141f, 148.342f, 66.000f, (sbyte)0);
                    break;
                case 6:
                    protector = (Npc)Spawn(282716, 584.240f, 142.233f, 66.000f, (sbyte)0);
                    break;
            }
            isSmallEggProtectorSpawned = true;
        }
        else if (!isHugeEggProtectorSpawned && this.GetNpcId() == 282614)
        {
            SpawnEliteCommander(); // Random spawn SpawnEliteCommander to protect Egg
            isHugeEggProtectorSpawned = true;
        }
    }

    private void SpawnEliteCommander()
    {
        protector = (Npc)RndSpawnInRange(282712, 5);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (this.GetNpcId())
        {
            case 282613:
                SmallEggSpawn();
                break;
            case 282614:
                HugeEggSpawn();
                break;
        }
    }

    private void SmallEggSpawn()
    {
        hatchTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                AIActions.DeleteOwner(this);
                AttackPlayer((Npc)Spawn(282616, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0));
            }
            return ValueTask.CompletedTask;
        }, HatchMillis);
    }

    private void HugeEggSpawn()
    {
        hatchTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                AIActions.DeleteOwner(this);
                AttackPlayer((Npc)Spawn(282620, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0));
            }
            return ValueTask.CompletedTask;
            // Retail's set_idle_timer is 60000 on both eggs; the huge egg's 120000 was a guess.
        }, HatchMillis);
    }

    private void AttackPlayer(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Npc padma = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(218756);
            if (padma != null)
            {
                npc.SetTarget(padma.GetTarget());
                npc.GetAi().SetStateIfNot(AIState.WALKING);
                npc.SetState(CreatureState.ACTIVE, true);
                npc.GetMoveController().MoveToTargetObject();
                PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
            }
            return ValueTask.CompletedTask;
        }, 1000L);
    }
}
