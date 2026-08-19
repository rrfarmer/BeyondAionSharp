using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/TwinDoorDestroyerAI (@author Estrayl).</summary>
/// <remarks>
/// Retail-sourced addition; see docs/retail-ai-fidelity.md. Retail's <c>IDSeal_Scene_06_Bomber_*</c>
/// ends its run with <c>is_last_waypoint</c> and two actions: it puts a <c>Scene_08</c> bomber on its
/// own mark and <b>despawns itself</b>. The successor is the npc that stays by the ruined door —
/// it greets the raid on <c>on_see_user</c> and answers message 22774 — and <b>nothing in this port
/// ever placed it</b>, so the door was destroyed by a demolisher that then stood there for ever.
/// <para>
/// <b>The gate attack is kept and is ours.</b> Retail opens the door through scene variables
/// (<c>set_condition_spawn_variable SCENE set=7</c>) which this port does not model; the Java class
/// casts 20840 from the gate npc instead, and that is what actually opens it here. So the handoff is
/// added <i>after</i> the attack rather than in place of it — retail's order would remove the
/// demolisher before our door opened.
/// </para>
/// <para>
/// <b>Not translated:</b> the successor's own pattern — a greeting broadcast on 22771 and a cast on
/// message 22774 against an unresolvable skill index.
/// </para>
/// </remarks>
[AIName("twin_door_destroyer")]
public class TwinDoorDestroyerAI : GeneralNpcAI
{
    /// <summary>Retail's <c>Scene_08</c> successors: Elyos demolisher to Elyos bomber, and likewise dark.</summary>
    internal const int ElyosDemolisher = 209690;
    internal const int ElyosSuccessor = 209697;
    internal const int AsmodianDemolisher = 209755;
    internal const int AsmodianSuccessor = 209762;

    /// <summary>The successor this demolisher hands off to, or 0 if it is not one of the two.</summary>
    internal static int SuccessorFor(int npcId) => npcId switch
    {
        ElyosDemolisher => ElyosSuccessor,
        AsmodianDemolisher => AsmodianSuccessor,
        _ => 0,
    };

    private readonly AtomicBoolean isGateReached = new AtomicBoolean();

    public TwinDoorDestroyerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        RemoveTrap();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501309, 2500);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop())
        {
            if (isGateReached.CompareAndSet(false, true))
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501310);
                ScheduleGateAttack();
            }
        }
    }

    private void RemoveTrap()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(207128, 207129))
                npc.GetController().Delete();
            return ValueTask.CompletedTask;
        }, 1500L);
    }

    private void ScheduleGateAttack()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(731580))
            {
                if (IsInRange(npc, 10))
                    SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 20840, 1, npc).UseWithoutPropSkill();
            }

            PacketSendUtility.BroadcastMessage(GetOwner(), 1501311);
            HandOver();
            return ValueTask.CompletedTask;
        }, 3500L);
    }

    /// <summary>Retail's last-waypoint pair: the successor on this mark, and this npc gone.</summary>
    private void HandOver()
    {
        int successor = SuccessorFor(GetNpcId());
        if (successor == 0)
            return;

        Spawn(successor, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading());
        AIActions.DeleteOwner(this);
    }
}
