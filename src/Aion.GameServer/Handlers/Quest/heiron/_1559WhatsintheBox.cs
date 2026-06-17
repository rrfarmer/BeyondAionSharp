using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author VladimirZ
    /// </summary>
    public class _1559WhatsintheBox : AbstractQuestHandler
    {
        public _1559WhatsintheBox() : base(1559)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestItem(182201823, questId);
            qe.RegisterQuestNpc(700513).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798072).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204571).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798013).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            int targetId = 0;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
            if (targetId == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                    return true;
                }
            }
            else if (targetId == 700513)
            {
                if (qs == null || qs.IsStartable())
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            if (player.GetInventory().GetItemCountByItemId(182201823) == 0)
                            {
                                return GiveQuestItem(env, 182201823, 1);
                            }
                            break;
                    }
                }
            }
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798072)
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
            if (targetId == 798072)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (ChangeQuestStep(env, 0, 1))
                            return CloseDialogWindow(env);
                        return false;
                }
            }
            else if (targetId == 204571)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        if (ChangeQuestStep(env, 1, 2))
                            return CloseDialogWindow(env);
                        return false;
                }
            }
            else if (targetId == 798013)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SETPRO3:
                        if (ChangeQuestStep(env, 2, 3, true))
                        {
                            RemoveQuestItem(env, 182201823, 1);
                            GiveQuestItem(env, 182201824, 1);
                            return CloseDialogWindow(env);
                        }
                        return false;
                }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.IsStartable())
            {
                return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
            }
            return HandlerResult.FAILED;
        }
    }
}
