using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1471FakeStigma : AbstractQuestHandler
{
    public _1471FakeStigma() : base(1471)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203991).AddOnQuestStart(questId); // Dionera
        qe.RegisterQuestNpc(203991).AddOnTalkEvent(questId); // Dionera
        qe.RegisterQuestNpc(203703).AddOnTalkEvent(questId); // Likesan
        qe.RegisterQuestNpc(798321).AddOnTalkEvent(questId); // Koruchinerk
        qe.RegisterQuestNpc(798024).AddOnTalkEvent(questId); // Kierunerk
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203991)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var0 = qs.GetQuestVarById(0);
            if (targetId == 203703)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var0 == 0)
                            return SendQuestDialog(env, 1352);
                        else if (var0 == 3)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return DefaultCloseDialog(env, 3, 3, true, true);
                }
            }
            else if (targetId == 798024)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1693);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 798321)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2034);
                    case DialogAction.SETPRO3:
                        return DefaultCloseDialog(env, 2, 3);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203703)
            {
                return SendQuestEndDialog(env);
            }
        }

        return false;
    }
}
