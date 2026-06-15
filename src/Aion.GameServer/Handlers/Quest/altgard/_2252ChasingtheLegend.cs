using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Majka
/// </summary>
public class _2252ChasingtheLegend : AbstractQuestHandler
{
    private static readonly int questStartNpcId = 203646; // Sinood
    private static readonly int questStep1NpcId = 700060; // Bones of Munishan (Npc)
    private static readonly int questActionItemId = 182203235; // Bones of Munishan (Item)
    private static readonly int questKillNpc1Id = 210634; // Minushan's Spirit
    private static readonly int questKillNpc2Id = 210635; // Minushan Drakie. To check on retail if it is spawned also 210635

    public _2252ChasingtheLegend() : base(2252)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(questStartNpcId).AddOnQuestStart(questId); // Sinood
        qe.RegisterQuestNpc(questStartNpcId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(questStep1NpcId).AddOnTalkEvent(questId); // Bone of Minusha
        qe.RegisterQuestNpc(questKillNpc1Id).AddOnKillEvent(questId); // Minushan's Spirit
        qe.RegisterQuestNpc(questKillNpc2Id).AddOnKillEvent(questId); // Minushan Drakie
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == questStartNpcId)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (QuestService.StartQuest(env))
                    {
                        GiveQuestItem(env, questActionItemId, 1);
                        return SendQuestDialog(env, 1003);
                    }
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == questStartNpcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            if (GiveQuestItem(env, questActionItemId, 1)) // Player hasn't action item; dialogue for a new chance
                            {
                                qs.SetQuestVarById(0, 0);
                                return SendQuestDialog(env, 1693);
                            }
                            else // Dialogue for encouragement
                            {
                                return SendQuestDialog(env, 2034);
                            }
                        }
                        break;
                }
            }
            if (targetId == questStep1NpcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (player.GetWorldMapInstance().GetNpcs(questKillNpc1Id, questKillNpc2Id).Count != 0)
                            return false;

                        if (var == 0 && CheckItemExistence(env, questActionItemId, 1, true))
                        {
                            // Random spawn
                            int chance = 95; // Chance to spawn biggest reward mob
                            int spawnTime = 3; // 3 min of spawn
                            int questSpawnedNpcId = Rnd.Chance() < chance ? questKillNpc1Id : questKillNpc2Id;
                            VisibleObject npc = env.GetVisibleObject();
                            Npc questMob = (Npc)SpawnTemporarily(questSpawnedNpcId, npc.GetWorldMapInstance(), npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading(), spawnTime); // Minushan's Spirit or Minushan's Drakie
                            PacketSendUtility.BroadcastMessage(questMob, 1100630, 500);
                            // TODO: set not usable icon to questStep1NpcId while mob is spawned, setting usable icon after mob is despawned
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            int var = qs.GetQuestVarById(0);

            switch (dialogActionId)
            {
                case DialogAction.SELECT_QUEST_REWARD:
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    qs.SetRewardGroup(var - 1);
                    return SendQuestEndDialog(env);
                default:
                    return SendQuestDialog(env, 1352);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }

        int var = qs.GetQuestVarById(0);
        if (var == 0)
        {
            int targetId = env.GetTargetId();
            if (targetId == questKillNpc1Id) // Minushan's Spirit - Highest reward
            {
                qs.SetStatus(QuestStatus.REWARD);
                qs.SetQuestVarById(0, 1);
                UpdateQuestStatus(env);
                return true;
            }
            else if (targetId == questKillNpc2Id) // Minushan Drakie - Lowest reward
            {
                qs.SetStatus(QuestStatus.REWARD);
                qs.SetQuestVarById(0, 2);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
