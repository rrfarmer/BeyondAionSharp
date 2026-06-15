using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _28830InteriorDecorator : AbstractQuestHandler
{
    private static readonly List<int> butlers = new List<int> { 810022, 810023, 810024, 810025, 810026 };

    public _28830InteriorDecorator() : base(28830)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830585).AddOnQuestStart(questId);
        foreach (int butlerId in butlers)
            qe.RegisterQuestNpc(butlerId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830651).AddOnTalkEvent(questId);
        qe.RegisterQuestHouseItem(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        House house = player.GetActiveHouse();

        if (house == null)
        {
            return false;
        }

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830585)
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
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == 0 && butlers.Contains(targetId))
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 830651)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ANOTHER_SINGLE_STEP_NOT_COMPLETED());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 830651)
        {
            switch (dialogActionId)
            {
                case DialogAction.USE_OBJECT:
                    return SendQuestDialog(env, 2375);
                case DialogAction.SELECT_QUEST_REWARD:
                    return SendQuestDialog(env, 5);
                case DialogAction.SELECTED_QUEST_NOREWARD:
                    SendQuestEndDialog(env);
                    return true;
            }
        }

        return false;
    }

    public override bool OnHouseItemUseEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
        {
            ChangeQuestStep(env, 1, 1, true);
        }
        return false;
    }
}
