using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas
/// </summary>
public class _18821AlmostForgotMyBlessings : AbstractQuestHandler
{
    private static readonly HashSet<int> butlers;

    static _18821AlmostForgotMyBlessings()
    {
        butlers = new HashSet<int>();
        butlers.Add(810017);
        butlers.Add(810018);
        butlers.Add(810019);
        butlers.Add(810020);
        butlers.Add(810021);
    }

    public _18821AlmostForgotMyBlessings() : base(18821)
    {
    }

    public override void Register()
    {
        IEnumerator<int> iter = butlers.GetEnumerator();
        while (iter.MoveNext())
        {
            int butlerId = iter.Current;
            qe.RegisterQuestNpc(butlerId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(butlerId).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();

        if (!butlers.Contains(targetId))
            return false;

        House house = player.GetActiveHouse();
        if (house == null || house.GetButler() == null || house.GetButler().GetNpcId() != targetId)
            return false;

        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 1011);
                case DialogAction.QUEST_ACCEPT_1:
                case DialogAction.QUEST_ACCEPT_SIMPLE:
                    return SendQuestStartDialog(env);
                case DialogAction.QUEST_REFUSE_1:
                case DialogAction.QUEST_REFUSE_SIMPLE:
                    return SendQuestDialog(env, 1004);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 2375);
                case DialogAction.SELECT_QUEST_REWARD:
                    ChangeQuestStep(env, 0, 0, true);
                    return SendQuestDialog(env, 5);
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (dialogActionId)
            {
                case DialogAction.USE_OBJECT:
                    return SendQuestDialog(env, 5);
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    SendQuestEndDialog(env);
                    return true;
            }
        }

        return false;
    }
}
