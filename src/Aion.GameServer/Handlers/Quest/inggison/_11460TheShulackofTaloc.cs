using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _11460TheShulackofTaloc : AbstractQuestHandler
{
    public _11460TheShulackofTaloc() : base(11460)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798954).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798954).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799502).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798985).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798954) // Tialla
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 799502: // Dorkin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 182209509, 1, 0, 0); // 1
                    }
                    break;
                case 798985: // Seikin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (RemoveQuestItem(env, 182209509, 1))
                                ChangeQuestStep(env, 1, 1, true); // reward
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798985) // Seikin
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
