using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/CelestiusAI (@author xTz).</summary>
[AIName("celestius")]
public class CelestiusAI : AggressiveNpcAI
{
    /// <summary>
    /// Retail <c>Elim_ComadAe</c> gives all three summons thirty seconds. They walk a path, so without
    /// it three more joined the patrol every time the branch fired and none ever left it.
    /// </summary>
    private const int SummonLife = 30;

    private const int SUMMONS_ID = 281514;
    private readonly AtomicBoolean isSpawnTaskStarted = new AtomicBoolean();
    private ScheduledTask? helpersTask;

    public CelestiusAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if ((creature is Player || creature is Summon) && isSpawnTaskStarted.CompareAndSet(false, true))
            StartHelpersCall();
    }

    private void StartHelpersCall()
    {
        helpersTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18981, 44, GetOwner()).UseNoAnimationSkill();
                StartWalker((Npc)SpawnFor(SUMMONS_ID, 518, 813, 1378, (sbyte)0, SummonLife), "3001900001");
                StartWalker((Npc)SpawnFor(SUMMONS_ID, 551, 795, 1376, (sbyte)0, SummonLife), "3001900002");
                StartWalker((Npc)SpawnFor(SUMMONS_ID, 574, 854, 1375, (sbyte)0, SummonLife), "3001900003");
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(1000), System.TimeSpan.FromMilliseconds(25000));
    }

    private void StartWalker(Npc npc, string walkId)
    {
        npc.GetSpawn().SetWalkerId(walkId);
        WalkManager.StartWalking((NpcAI)npc.GetAi());
        npc.SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
    }

    private void CleanUp()
    {
        CancelTask();
        DeleteSummons();
    }

    private void CancelTask()
    {
        if (helpersTask != null && !helpersTask.IsDone())
        {
            helpersTask.Cancel(true);
        }
    }

    private void DeleteSummons()
    {
        GetPosition().GetWorldMapInstance().GetNpcs(SUMMONS_ID).ToList().ForEach(npc => npc.GetController().Delete());
    }

    protected override void HandleBackHome()
    {
        CleanUp();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CleanUp();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CleanUp();
        base.HandleDied();
    }
}
