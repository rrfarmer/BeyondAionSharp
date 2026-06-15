using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _30211GroupTheRodandtheOrb : AbstractQuestHandler
{
    public _30211GroupTheRodandtheOrb() : base(30211)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798941).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798941).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730275).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798941)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 730275:
                    switch (dialogActionId)
                    {
                        case DialogAction.SETPRO1:
                        case DialogAction.QUEST_SELECT:
                            RemoveQuestItem(env, 182209614, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return true;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798941)
            {
                return dialogActionId switch
                {
                    DialogAction.USE_OBJECT => SendQuestDialog(env, 10002),
                    DialogAction.SELECT_QUEST_REWARD => SendQuestDialog(env, 5),
                    _ => SendQuestEndDialog(env),
                };
            }
        }
        return false;
    }
}
