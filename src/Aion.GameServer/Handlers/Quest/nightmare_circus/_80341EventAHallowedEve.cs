using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _80341EventAHallowedEve : AbstractQuestHandler
{
    public _80341EventAHallowedEve() : base(80341)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831709).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831710).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831711).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831712).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831713).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831714).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831715).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831716).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831717).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831709).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831710).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831711).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831712).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831713).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831714).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831715).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831716).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831717).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        bool isTargetValidQuestNpc = targetId == 831709 || targetId == 831710 || targetId == 831711 || targetId == 831712 || targetId == 831713
            || targetId == 831714 || targetId == 831715 || targetId == 831716 || targetId == 831717;

        if (qs == null || qs.IsStartable())
        {
            if (isTargetValidQuestNpc)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (isTargetValidQuestNpc)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return var == 0 && SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 0, 0, true);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (isTargetValidQuestNpc)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
