using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1107TheLostAxe : AbstractQuestHandler
{
    public _1107TheLostAxe() : base(1107)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203075).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = env.GetTargetId();
        if (targetId == 0)
        {
            if (qs == null || qs.IsStartable())
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        QuestService.StartQuest(env);
                        return CloseDialogWindow(env);
                    case DialogAction.QUEST_REFUSE_1:
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 203075)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    RemoveQuestItem(env, 182200501, 1);
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
