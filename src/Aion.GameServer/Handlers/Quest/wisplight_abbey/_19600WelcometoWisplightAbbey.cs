using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _19600WelcometoWisplightAbbey : AbstractQuestHandler
{
    private const int npcId = 804651; // Lena
    private const int itemId = 164000335; // Abbey Return Stone

    public _19600WelcometoWisplightAbbey() : base(19600)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(itemId, questId); // quest acquisition linked to abbey return stone acquisition
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == npcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return DefaultCloseDialog(env, 0, 0, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcId)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return QuestService.StartQuest(env);
    }
}
