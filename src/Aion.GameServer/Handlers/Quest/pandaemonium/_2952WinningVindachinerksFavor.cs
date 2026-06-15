using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2952WinningVindachinerksFavor : AbstractQuestHandler
{
    public _2952WinningVindachinerksFavor() : base(2952)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279006).AddOnQuestStart(questId); // Garkbinerk
        qe.RegisterQuestNpc(279006).AddOnTalkEvent(questId); // Garkbinerk
        qe.RegisterQuestNpc(279016).AddOnTalkEvent(questId); // Vindachinerk
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 279006)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVars().GetQuestVars();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 279016: // Vindachinerk
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 0, 0, true, 5, 2716); // reward
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 0, 0);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 279016)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
