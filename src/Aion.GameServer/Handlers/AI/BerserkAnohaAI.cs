using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/kaldor/BerserkAnohaAI (@author Ritsu, Estrayl).</summary>
[AIName("berserk_anoha")]
public class BerserkAnohaAI : AggressiveNpcAI
{
    private SiegeRace occupier;

    public BerserkAnohaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_SPAWN());
        ScheduleDespawn();
        occupier = SiegeService.GetInstance().GetFortress(7011).GetRace();
    }

    private void ScheduleDespawn()
    {
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromHours(1)));
    }

    protected override void HandleDespawned()
    {
        if (!IsDead())
            PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_DESPAWN());
        DespawnFlag();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        GetOwner().GetController().CancelTask(TaskId.DESPAWN);
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_DIE());
        DespawnFlag();
        CheckForFactionReward();
        base.HandleDied();
    }

    private void DespawnFlag()
    {
        Npc flag = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(702618); // see AnohasSword AI
        if (flag != null)
            flag.GetController().Delete();
    }

    private void CheckForFactionReward()
    {
        Npc ca = (Npc)Spawn(occupier == SiegeRace.ASMODIANS ? 804594 : 804595, 785.4833f, 458.4128f, 143.7177f, (sbyte)30); // Commander Anoha
        ca.GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(_ => { ca.GetController().Delete(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(60)));
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        if (creature is Player player)
        {
            if (occupier == SiegeRace.ASMODIANS)
            {
                StartQuest(player, creature.GetRace() == Race.ELYOS ? 13818 : 23817);
            }
            else if (occupier == SiegeRace.ELYOS)
            {
                StartQuest(player, creature.GetRace() == Race.ELYOS ? 13817 : 23818);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }

    private void StartQuest(Player player, int questId)
    {
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        QuestEnv env = new QuestEnv(null, player, questId);
        if (qs == null || qs.IsStartable())
            QuestService.StartQuest(env);
    }
}
