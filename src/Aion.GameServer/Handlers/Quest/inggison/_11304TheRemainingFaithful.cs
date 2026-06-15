using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author bobobear Talk to Pilomenes. Kill Isbariya the Resolute at Beshmundir Temple and find the Balaur orders. Afterwards, take it to Outremus.
/// </summary>
public class _11304TheRemainingFaithful : AbstractQuestHandler
{
    public _11304TheRemainingFaithful() : base(11304)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798927).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798927).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798926).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798941).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(182215336, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798927)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                    {
                        return SendQuestStartDialog(env);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798941:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        case DialogAction.SETPRO1:
                        {
                            return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798926)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            ChangeQuestStep(env, 1, 2, true); // reward
            return true;
        }
        return false;
    }
}
