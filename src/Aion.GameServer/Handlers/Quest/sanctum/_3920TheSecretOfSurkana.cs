using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Put the Inactivated Surkana inside the Balaur Material Converter (730212). Talk with Shoshinerk (798357).
/// @author Bobobear
/// </summary>
public class _3920TheSecretOfSurkana : AbstractQuestHandler
{
    public _3920TheSecretOfSurkana() : base(3920)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798357).AddOnQuestStart(questId); // Shoshinerk
        qe.RegisterQuestNpc(798357).AddOnTalkEvent(questId); // Shoshinerk
        qe.RegisterQuestNpc(730212).AddOnTalkEvent(questId); // Balaur Material Converter
        qe.RegisterQuestItem(182206073, questId);
        qe.RegisterQuestItem(182206074, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798357)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, 182206073, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 730212: // Balaur Material Converter
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if ((var == 0) && player.GetInventory().GetItemCountByItemId(182206073) > 0)
                            {
                                return UseQuestObject(env, 0, 1, true, 0, 182206074, 1, 182206073, 1, 0, false);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798357) // Shoshinerk
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
