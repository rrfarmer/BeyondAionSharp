using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _21460AShulacksStory : AbstractQuestHandler
{
    public _21460AShulacksStory() : base(21460)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799258).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799258).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799502).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799276).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799258) // Denskel
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
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
                            return DefaultCloseDialog(env, 0, 1, 182209520, 1, 0, 0); // 1
                    }
                    break;
                case 799276: // Chenkiki
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (RemoveQuestItem(env, 182209520, 1))
                                ChangeQuestStep(env, 1, 1, true); // reward
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799276) // Chenkiki
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
