using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3058StoneofMabolo : AbstractQuestHandler
{
    public _3058StoneofMabolo() : base(3058)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798189).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203701).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798213).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (env.GetDialogActionId() == DialogAction.ASK_QUEST_ACCEPT)
            {
                return SendQuestDialog(env, 4);
            }
            else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                return SendQuestStartDialog(env);
            }
            else if (env.GetDialogActionId() == DialogAction.QUEST_REFUSE_1)
            {
                return CloseDialogWindow(env);
            }
        }
        if (qs == null)
            return false;

        if (targetId == 798189)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 1352);
                case DialogAction.SETPRO1:
                    return DefaultCloseDialog(env, 0, 1);
            }
        }
        else if (targetId == 203701)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 1693);
                case DialogAction.SETPRO2:
                    return DefaultCloseDialog(env, 1, 2);
            }
        }
        else if (targetId == 798213)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 2375);
                case DialogAction.SELECT_QUEST_REWARD:
                    ChangeQuestStep(env, 2, 2, true);
                    break;
            }
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
