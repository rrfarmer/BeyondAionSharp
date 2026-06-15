using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _24112NoLaissezFaireForLepharists : AbstractQuestHandler
{
    private static readonly int questStartNpcId = 203631; // Nokir
    private static readonly int questEndNpcId = 832821; // Brodir
    private static readonly int questKillNpcId = 210510; // Comrade Sumarhon

    public _24112NoLaissezFaireForLepharists() : base(24112)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(questStartNpcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(questStartNpcId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(questEndNpcId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(questKillNpcId).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
        {
            targetId = npc.GetNpcId();
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (targetId == questStartNpcId) // Nokir
        {
            if (qs == null || qs.IsStartable())
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (targetId == questEndNpcId) // Brodir
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);

                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        else if (var == 1)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        qs.SetQuestVarById(0, 1);
                        return SendQuestSelectionDialog(env);
                    case DialogAction.SELECT_QUEST_REWARD:
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestEndDialog(env);
                }
            }

            if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
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

        int targetId = env.GetTargetId();
        int var = qs.GetQuestVarById(0);

        if (var == 0 && targetId == questKillNpcId)
        {
            qs.SetQuestVarById(0, 1);
            UpdateQuestStatus(env);
            return true;
        }
        return false;
    }
}
