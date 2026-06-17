using System.Threading.Tasks;
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
    /// @author Aion Gates
    /// </summary>
    public class _4060TheZombiesDescendant : AbstractQuestHandler
    {
        public _4060TheZombiesDescendant() : base(4060)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(205156).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204143).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204731).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(205204).AddOnTalkEvent(questId);
            qe.RegisterQuestItem(182209037, questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            int targetId = 0;
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
            else if (targetId == 205156)
            {
                if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1352);
                    else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    {
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }
            else if (targetId == 204143)
            {
                if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1693);
                    else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                    {
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }
            else if (targetId == 204731)
            {
                if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2034);
                    else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                    {
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }
            else if (targetId == 205204)
            {
                if (qs != null)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                    {
                        RemoveQuestItem(env, 182209037, 1);
                        qs.SetQuestVar(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestEndDialog(env);
                    }
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            int id = item.GetItemTemplate().GetTemplateId();
            int itemObjId = item.GetObjectId();

            if (id != 182209037)
                return HandlerResult.UNKNOWN;
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
                SendQuestDialog(env, 4);
                return ValueTask.CompletedTask;
            }, 3000L);
            return HandlerResult.SUCCESS;
        }
    }
}
