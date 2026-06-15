using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, Bobobear
/// </summary>
public class _28821YourButlerGift : AbstractQuestHandler
{
    private static readonly HashSet<int> butlers;

    static _28821YourButlerGift()
    {
        butlers = new HashSet<int>();
        butlers.Add(810022);
        butlers.Add(810023);
        butlers.Add(810024);
        butlers.Add(810025);
        butlers.Add(810026);
    }

    public _28821YourButlerGift() : base(28821)
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
                    SendQuestEndDialog(env);
                    return true;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (dialogActionId)
            {
                case DialogAction.USE_OBJECT:
                    return SendQuestDialog(env, 5);
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    return SendQuestEndDialog(env);
            }
        }

        return false;
    }
}
