using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2663AcquiringanAntidote : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204777, 204790 };

    public _2663AcquiringanAntidote() : base(2663)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204777).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204777)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204777)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 204790)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1352);
                    return false;
                case DialogAction.SETPRO1:
                    return DefaultCloseDialog(env, 0, 1, true, false, workItems[0]);
            }
        }
        return false;
    }
}
