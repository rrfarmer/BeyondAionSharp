using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, bobobear
/// </summary>
public class _28828TheManyFacetsOfFriendship : AbstractQuestHandler
{
    private static readonly HashSet<int> butlers;

    static _28828TheManyFacetsOfFriendship()
    {
        butlers = new HashSet<int>();
        butlers.Add(810022);
        butlers.Add(810023);
        butlers.Add(810024);
        butlers.Add(810025);
        butlers.Add(810026);
    }

    public _28828TheManyFacetsOfFriendship() : base(28828)
    {
    }

    public override void Register()
    {
        foreach (int butlerId in butlers)
        {
            qe.RegisterQuestNpc(butlerId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(butlerId).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestItem(182213205, questId);
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
                    return SendQuestStartDialog(env, 182213205, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (dialogActionId)
            {
                case DialogAction.USE_OBJECT:
                    return SendQuestDialog(env, 2375);
                case DialogAction.SELECT_QUEST_REWARD:
                    return SendQuestEndDialog(env, new int[] { 182213205 });
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        if (id == 182213205)
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                qs.SetQuestVar(1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
        }
        return HandlerResult.UNKNOWN;
    }
}
