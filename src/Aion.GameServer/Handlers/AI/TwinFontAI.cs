using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The lava font (855708) and heatvent font (855709) a dying twin protector leaves behind. Retail
/// patterns <c>IDSeal_Twin_P_Source</c> and <c>IDSeal_Twin_M_Source</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_retail_messages.py</c> — messages <b>22704</b> and <b>22705</b>,
/// which its `no speaker` verdict had already shown were unreachable for a spawn-data reason rather
/// than a porting one.
/// <para>
/// <b>Told its change has failed, a font calls your own side's guards down onto itself.</b> Two
/// soldiers at five metres and their leader at six, which then destroy it — the soldiers with a
/// million hate and the leader with a hundred thousand, so nothing peels them.
/// </para>
/// <para>
/// <b>Nothing in our instance sends that message yet, and an earlier version of this file said
/// otherwise.</b> The font is a thing that changes into something else, and retail has a separate
/// announcer for each outcome: <c>22701</c> turns it into the strong protector, <c>22707</c> into the
/// fountless one, <c>22709</c> into a quest object, and <c>22704</c>/<c>22705</c> — these — say the
/// change failed and bring the guards. The fifteen-second window our instance measures is the
/// <c>22707</c> moment, not this one, and it is now translated directly in
/// <c>DrakenspireDepthsInstance.OnTwinRespawn</c>. The state this handler answers is a font left
/// standing with no outcome at all, which our instance never produces. Kept because it is a correct
/// translation and the day that state exists it will work; recorded as a listener without a sender
/// rather than left looking wired.
/// </para>
/// <para>
/// <b>The guards are your race, not the boss's.</b> Retail splits the branch on <c>is_race</c> and
/// ships two of everything: the Elyos detachment (209688 leader, 209689 soldier) and the Asmodian one
/// (209753, 209754). Read here from the players actually in the instance rather than from the font,
/// which has no race of its own.
/// </para>
/// <para>
/// <b>Once.</b> Retail's branch carries a flag var, and the display keeps announcing every three
/// seconds until something tells it to stop, so without it a failed raid would drown in guards.
/// </para>
/// <para>
/// <b>Not translated:</b> this pattern's other handlers — 22708 and 22709, the success messages, which
/// swap the font for a quest object, and 22717, which clears the protector's own summons. All three
/// belong to instance sequencing our handler drives its own way.
/// </para>
/// </remarks>
[AIName("twin_font")]
public class TwinFontAI : AggressiveNoLootNpcAI, INpcMessageListener
{
    /// <summary>The lava font's time-over, and the heatvent font's.</summary>
    public const int PhysicalTimeOver = 22704;
    public const int MagicalTimeOver = 22705;

    /// <summary><c>IDSeal_Scene_05_PCGuard_*</c>, leader and soldier, one pair per race.</summary>
    private const int ElyosLeader = 209688;
    private const int ElyosSoldier = 209689;
    private const int AsmodianLeader = 209753;
    private const int AsmodianSoldier = 209754;

    private const int Soldiers = 2;
    private const float SoldierRing = 5f;
    private const float LeaderRing = 6f;

    /// <summary>Retail's <c>hatepoints_to_add</c>: absurd on purpose, so nothing peels them.</summary>
    private const int SoldierHate = 1_000_000;
    private const int LeaderHate = 100_000;

    private bool guardsCalled;

    public TwinFontAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Which time-over this font answers. The lava one is physical, the heatvent magical.</summary>
    internal static int TimeOverFor(int npcId) => npcId switch
    {
        855708 => PhysicalTimeOver,
        855709 => MagicalTimeOver,
        _ => 0,
    };

    /// <summary>The detachment that answers for a given race: leader first, then the soldier.</summary>
    internal static (int Leader, int Soldier) DetachmentFor(Race race) =>
        race == Race.ASMODIANS ? (AsmodianLeader, AsmodianSoldier) : (ElyosLeader, ElyosSoldier);

    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != TimeOverFor(GetOwner().GetNpcId()) || IsDead() || guardsCalled)
            return;

        guardsCalled = true;
        (int leader, int soldier) = DetachmentFor(RaceInside());

        for (int i = 0; i < Soldiers; i++)
            CallGuard(soldier, SoldierRing, SoldierHate);

        CallGuard(leader, LeaderRing, LeaderHate);
    }

    /// <summary>
    /// Whose detachment answers. Read from the instance's own players; Elyos when it cannot tell,
    /// which only happens if the last of them left between the time-over and this call.
    /// </summary>
    private Race RaceInside()
    {
        List<Player> inside = GetPosition().GetWorldMapInstance()?.GetPlayersInside() ?? new List<Player>();
        return inside.Count == 0 ? Race.ELYOS : inside[0].GetRace();
    }

    private void CallGuard(int npcId, float ring, int hate)
    {
        WorldPosition here = GetPosition();
        double angle = Rnd.NextFloat(360f) * System.Math.PI / 180.0;
        float x = here.GetX() + (float)(System.Math.Cos(angle) * ring);
        float y = here.GetY() + (float)(System.Math.Sin(angle) * ring);

        if (Spawn(npcId, x, y, here.GetZ(), (sbyte)here.GetHeading()) is Npc guard)
            AttackAfterSpawn.NextTick(guard, GetOwner(), hate);
    }
}

/// <summary>
/// The failure display that announces a twin-protector time-over (855510, 855511, 856403, 856404).
/// Retail patterns <c>IDSeal_Twin_P_Change_Failed</c> and <c>IDSeal_Twin_M_Change_Failed</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its whole job is to say the raid ran out of time:
/// broadcast on waking, and again every <b>three seconds</b> until something dismisses it. Our
/// instance already knows the moment — <c>DrakenspireDepthsInstance.OnTwinRespawn</c> runs fifteen
/// seconds after a twin dies and checks whether the font is still standing — and nothing was placed
/// there, which is why <see cref="TwinFontAI"/>'s handler had no sender.
/// <para>
/// <b>It stops itself.</b> Retail's dismissal is message 22696, sent by a quest guard and a scene
/// NPC neither of which this work has reached, so it would announce forever. It is given the twenty
/// seconds its font needs instead, and that substitution is the one invented number here.
/// </para>
/// </remarks>
[AIName("twin_failure_display")]
public class TwinFailureDisplayAI : GeneralNpcAI
{
    private const int Physical = 22704;
    private const int Magical = 22705;

    /// <summary>Retail's <c>range_as_meter</c> and its <c>set_idle_timer</c>.</summary>
    public const float Reach = 50f;
    private const int RepeatMillis = 3000;

    /// <summary>See the remarks: ours, in place of a dismissal we cannot receive.</summary>
    private const int LifetimeMillis = 20000;

    private ScheduledTask? announcing;

    public TwinFailureDisplayAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Which time-over this display announces, or 0 for an npc that is not one.</summary>
    internal static int AnnouncementFor(int npcId) => npcId switch
    {
        855510 or 856403 => Physical,
        855511 or 856404 => Magical,
        _ => 0,
    };

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        int message = AnnouncementFor(GetOwner().GetNpcId());
        if (message == 0)
            return;

        announcing = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (GetOwner().IsSpawned() && !IsDead())
                NpcMessageBus.Broadcast(GetOwner(), message, null, Reach);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.Zero, System.TimeSpan.FromMilliseconds(RepeatMillis));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetOwner().IsSpawned())
                GetOwner().GetController().DeleteIfAliveOrCancelRespawn();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, LifetimeMillis);
    }

    protected override void HandleDespawned()
    {
        if (announcing != null && !announcing.IsDone())
            announcing.Cancel(true);
        announcing = null;
        base.HandleDespawned();
    }
}
