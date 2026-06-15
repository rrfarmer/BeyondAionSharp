using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14043DrawlingBalaur : AbstractQuestHandler
    {
        public _14043DrawlingBalaur() : base(14043)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestItem(182215351, questId);
            qe.RegisterQuestNpc(278532).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798026).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798025).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(279019).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14040);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 278532)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 278532)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            ChangeQuestStep(env, 0, 1);
                            int currentHour = GameTimeService.GetInstance().GetGameTime().GetHour();
                            if (currentHour < 8 || currentHour >= 20)
                                TeleportService.TeleportTo(player, 110010000, 1819.51f, 2189.24f, 528.52f, (byte)36, TeleportAnimation.FADE_OUT_BEAM);
                            else
                                TeleportService.TeleportTo(player, 110010000, 1964.69f, 1767.63f, 576.76f, (byte)2, TeleportAnimation.FADE_OUT_BEAM);
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 798026)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        else if (var == 4)
                            return SendQuestDialog(env, 2375);
                        else if (var == 6 || var == 8)
                            return SendQuestDialog(env, 3057);
                        return false;
                    case DialogAction.SETPRO5:
                        if (var == 4)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            RemoveQuestItem(env, 182202002, 1);
                            GiveQuestItem(env, 182215351, 1);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                    case DialogAction.SETPRO7:
                        if (var == 6 || var == 8)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            TeleportService.TeleportTo(player, 400010000, 2979.37f, 923.05f, 1538.92f, (byte)103, TeleportAnimation.FADE_OUT_BEAM);
                            return true;
                        }
                        return false;

                    case DialogAction.SETPRO11:
                        if (var == 1 && player.GetInventory().TryDecreaseKinah(20000))
                        {
                            if (!GiveQuestItem(env, 182215351, 1))
                                return true;
                            qs.SetQuestVar(7);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        else
                            return SendQuestDialog(env, 1355);
                    case DialogAction.SETPRO12:
                        if (var == 1)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 798025)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 279019)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SETPRO4:
                        if (var == 3)
                        {
                            if (!GiveQuestItem(env, 182202002, 1))
                                return true;
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }

            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            int id = item.GetItemTemplate().GetTemplateId();
            int itemObjId = item.GetObjectId();

            if (id != 182215351)
                return HandlerResult.UNKNOWN;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return HandlerResult.FAILED;

            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 1, 1, 0), true);
            RemoveQuestItem(env, 182215351, 1);
            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
            UpdateQuestStatus(env);
            return HandlerResult.SUCCESS;
        }
    }
}
