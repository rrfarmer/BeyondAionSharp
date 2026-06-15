using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _30553ComradesInArms : AbstractQuestHandler
{
    public _30553ComradesInArms() : base(30553)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205438).AddOnQuestStart(questId); // Lition
        qe.RegisterQuestNpc(205438).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(701097).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799541).AddOnTalkEvent(questId); // rodelion
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (qs == null || qs.IsStartable())
        {
            switch (targetId)
            {
                case 205438:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 4762);
                        default:
                            return SendQuestStartDialog(env);
                    }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 205438:
                    switch (dialogActionId)
                    {
                        case DialogAction.SELECT_QUEST_REWARD:
                            return DefaultCloseDialog(env, 1, 1, true, true);
                    }
                    return false;
                case 701097:
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().Delete();
                    return true;
                case 799541:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SET_SUCCEED:
                            {
                                return DefaultCloseDialog(env, 0, 1, true, false); // reward
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 205438:

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
