using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _1947ALuckyDay : AbstractQuestHandler
{
    public _1947ALuckyDay() : base(1947)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203795).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203795).AddOnTalkEvent(questId); // Demodocos
        qe.RegisterQuestNpc(798012).AddOnTalkEvent(questId); // Yiehmonerk
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable()) // TODO: If cube size is bigger than 5
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 1011);
                case DialogAction.ASK_QUEST_ACCEPT:
                    return SendQuestDialog(env, 4);
                case DialogAction.QUEST_ACCEPT_1:
                    env.SetQuestId(questId);
                    QuestService.StartQuest(env);
                    return SendQuestDialog(env, 1003);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVars().GetQuestVars();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798012: // Yiehmonerk
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 2375);
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (var == 0)
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 798012)
            return SendQuestEndDialog(env);
        return false;
    }
}
