using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, vlog
/// </summary>
public class _3965TotheGalleriaofGrandeur : AbstractQuestHandler
{
    public _3965TotheGalleriaofGrandeur() : base(3965)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798311).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798311).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798391).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798390).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798311) // Senarinrinerk
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182206120, 2);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 798391: // Andu
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 0, 0, 182206120, 1); // 1
                    }
                    break;
                case 798390: // Palentine
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 1, 1, true);
                            RemoveQuestItem(env, 182206120, 1);
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798390) // Palentine
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
