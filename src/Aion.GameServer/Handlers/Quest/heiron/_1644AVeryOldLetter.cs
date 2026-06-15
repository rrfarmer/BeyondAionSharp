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
    /// @author Balthazar, Gigi
    /// </summary>
    public class _1644AVeryOldLetter : AbstractQuestHandler
    {
        public _1644AVeryOldLetter() : base(1644)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204545).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204537).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204546).AddOnTalkEvent(questId);
            qe.RegisterQuestItem(182201765, questId);
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
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0));
                    return true;
                }
            }

            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 204545:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    if (qs.GetQuestVarById(0) == 0)
                                    {
                                        return SendQuestDialog(env, 1352);
                                    }
                                    else if (qs.GetQuestVarById(0) == 2)
                                    {
                                        return SendQuestDialog(env, 2034);
                                    }
                                    return false;
                                }
                            case DialogAction.SETPRO1:
                                {
                                    return DefaultCloseDialog(env, 0, 1);
                                }
                            case DialogAction.SETPRO3:
                                {
                                    qs.SetQuestVar(3);
                                    return DefaultCloseDialog(env, 3, 3, true, false);
                                }
                        }
                        return false;
                    case 204537:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                            case DialogAction.SETPRO2:
                                {
                                    return DefaultCloseDialog(env, 1, 2, 0, 0, 182201765, 1);
                                }
                        }
                        break;
                }
            }
            else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204546)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        default:
                            {
                                return SendQuestEndDialog(env);
                            }
                    }
                }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            int itemObjId = item.GetObjectId();
            int id = item.GetItemTemplate().GetTemplateId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (id != 182201765)
            {
                return HandlerResult.UNKNOWN;
            }

            if (qs != null)
            {
                if (qs.GetStatus() == QuestStatus.COMPLETE)
                {
                    RemoveQuestItem(env, 182201765, 1);
                    return HandlerResult.FAILED;
                }
            }

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
