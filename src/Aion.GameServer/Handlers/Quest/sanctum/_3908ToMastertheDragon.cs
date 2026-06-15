using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3908ToMastertheDragon : AbstractQuestHandler
{
    public _3908ToMastertheDragon() : base(3908)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798316).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798316).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700515).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798316)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env, 182206056, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700515)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (RemoveQuestItem(env, 182206056, 1))
                            Spawn(215384, player.GetWorldMapInstance(), (float)493.9681, (float)519.1524, (float)968.9094, (byte)0);
                        return true;
                }
            }
            else if (targetId == 798316)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (player.GetInventory().GetItemCountByItemId(182206057) >= 1)
                            return DefaultCloseDialog(env, 0, 0, true, true, 0, 0, 182206057, 1);
                        else
                            return SendQuestDialog(env, 10001);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
