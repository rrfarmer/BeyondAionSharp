using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Captain Xasta, Rentus Base. Retail pattern IDYun_Nmd3 (217309); his second form (217310) runs its
/// own pattern, which is not translated here.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His first form ran a 28s cycle that stopped him
/// attacking, walked him down a path, summoned two Inhibitor Sikars and ended in a sanctuary event.
/// None of that is in the pattern, which arms two battle timers and lets them run the fight:
/// <list type="bullet">
/// <item>every 9s, self-cast Dragon Breath and drop three Magic Flames on the current target;</item>
/// <item>every 6s, check HP and send one siege artilleryman the first time it passes 85/65/45/20.</item>
/// </list>
/// Both sets of adds share one spawn id, so leaving the fight clears them together.
/// <para>
/// The pattern addresses only skill index 0 of his two, and its branch is named Blaze: skill 19657
/// is Dragon Breath (stack <c>IDYUN_RASTA_BLAZE</c>) and the branch spawns <c>IDYun_3Nmd_Blaze</c>,
/// so the index resolves unambiguously. Index 1, Interception Soldier Shout, is the sanctuary shield
/// the old cycle applied; no branch casts it, so it stays listed but silent.
/// </para>
/// </remarks>
[AIName("captain_xasta")]
public class CaptainXastaAI : PatternAi
{
    private const int FirstFormNpcId = 217309;
    private const int SecondFormNpcId = 217310;

    /// <summary>Skill index 0 — Dragon Breath, cast at himself.</summary>
    private const int DragonBreath = 19657;

    private const int MagicFlame = 282390;
    private const int SiegeArtilleryman = 282606;

    /// <summary>Retail files the flames and the artillerymen together, so leaving the fight clears both.</summary>
    private const int Adds = 1;

    /// <summary>The four wave branches differ only in threshold and flag, so their actions are shared.</summary>
    private static PatternAction[] Wave() =>
    [
        Do.Say(1500389),
        Do.ArmTimer(2, 6000),
        Do.SpawnNear(SiegeArtilleryman, Adds, count: 1, range: 5f),
    ];

    private static readonly AiPattern FirstForm = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(8, "start Timer_0", When.Always,
                Do.Say(1500388),
                Do.ArmTimer(1, 6000),
                Do.ArmTimer(2, 6000))),

        OnBattleTimer = Of(
            Branch(6, "Blaze", [When.Timer(1)],
                Do.SkillOnSelf(DragonBreath),
                Do.SpawnOnTarget(MagicFlame, Adds, count: 3, range: 4f, liveSeconds: 15),
                Do.ArmTimer(1, 9000)),

            Branch(5, "wave_85%", [When.HpBelow(85), When.Timer(2), When.FirstTime(1)], Wave()),
            Branch(4, "wave_65%", [When.HpBelow(65), When.Timer(2), When.FirstTime(2)], Wave()),
            Branch(3, "wave_45%", [When.HpBelow(45), When.Timer(2), When.FirstTime(3)], Wave()),
            Branch(2, "wave_20%", [When.HpBelow(20), When.Timer(2), When.FirstTime(4)], Wave()),

            Branch(1, "RepeatTimer2", [When.Timer(2)],
                Do.ArmTimer(2, 6000))),

        OnEnterIdle = Of(
            Branch(14, "Despawn&Broad", When.Always,
                Do.Despawn(Adds))),

        OnDie = Of(
            Branch(13, "FallOff", When.Always,
                Do.Say(1500390),
                Do.SpawnAt(SecondFormNpcId, spawnId: 2, liveSeconds: 0,
                    new SpawnSpot(238.160f, 598.624f, 178.480f)),
                Do.Despawn(Adds),
                Do.DespawnSelf())),
    };

    /// <summary>His second form binds to its own pattern, which this class does not translate.</summary>
    private static readonly AiPattern SecondForm = new AiPattern();

    private ScheduledTask? secondFormTask;

    public CaptainXastaAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => GetNpcId() == FirstFormNpcId ? FirstForm : SecondForm;

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (GetNpcId() == SecondFormNpcId && secondFormTask == null)
            StartSecondFormTask();
    }

    /// <summary>The second form's 30s cast, kept exactly as it was.</summary>
    /// <remarks>
    /// 217310 binds to its own pattern, and translating that is separate work from the first form.
    /// Leaving it alone keeps the fight's second half behaving as it always has.
    /// </remarks>
    private void StartSecondFormTask()
    {
        secondFormTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelSecondFormTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19729, 60, GetOwner()).UseNoAnimationSkill();
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500392);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(30000), TimeSpan.FromMilliseconds(30000));
    }

    private void CancelSecondFormTask()
    {
        if (secondFormTask != null && !secondFormTask.IsDone())
            secondFormTask.Cancel(true);
        secondFormTask = null;
    }

    protected override void HandleDied()
    {
        CancelSecondFormTask();
        bool secondForm = GetNpcId() == SecondFormNpcId;
        base.HandleDied();
        if (secondForm)
            OnSecondFormDied();
    }

    protected override void HandleDespawned()
    {
        CancelSecondFormTask();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        CancelSecondFormTask();
        base.HandleBackHome();
    }

    /// <summary>Ariana's escort out of the instance, unchanged.</summary>
    private void OnSecondFormDied()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500391);
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance == null)
            return;

        Npc ariana = instance.GetNpc(799668);
        if (ariana == null)
            return;

        ariana.GetEffectController().RemoveEffect(19921);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            ariana.GetSpawn().SetWalkerId("30028000016");
            WalkManager.StartWalking((NpcAI)ariana.GetAi());
            return ValueTask.CompletedTask;
        }, 1000L);
        PacketSendUtility.BroadcastMessage(ariana, 1500415, 4000);
        PacketSendUtility.BroadcastMessage(ariana, 1500416, 13000);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(ariana, 19358, 60, ariana).UseNoAnimationSkill();
            instance.SetDoorState(145, true);
            foreach (Npc npc in instance.GetNpcs(701156))
                npc?.GetController().Delete();
            ThreadPoolManager.GetInstance().Schedule(_ => { ariana.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, 13000L);
            return ValueTask.CompletedTask;
        }, 13000L);
    }
}
