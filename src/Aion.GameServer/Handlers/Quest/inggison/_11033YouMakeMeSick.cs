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
    /// @author VladimirZ
    /// </summary>
    public class _11033YouMakeMeSick : AbstractQuestHandler
    {
        public _11033YouMakeMeSick() : base(11033)
        {
        }

        public override void Register()
        {
            int[] npcs = { 798959 };
            foreach (int npc in npcs)
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            qe.RegisterQuestItem(182206728, questId);
            qe.RegisterQuestNpc(798959).AddOnQuestStart(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            if (SendQuestNoneDialog(env, 798959, 4762))
                return true;

            Player player = env.GetPlayer();

            QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;
            int var = qs.GetQuestVarById(0);
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetTargetId() == 798959)
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
            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (env.GetTargetId())
                {
                    case 798959:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                else if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                if (var == 0)
                                {
                                    if (QuestService.CollectItemCheck(env, true))
                                    {
                                        qs.SetQuestVarById(0, var + 1);
                                        UpdateQuestStatus(env);
                                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                        return true;
                                    }
                                    else
                                        return SendQuestDialog(env, 10001);
                                }
                                return false;
                            case DialogAction.SETPRO2:
                                if (var == 1)
                                {
                                    if (!GiveQuestItem(env, 182206728, 1))
                                        return true;
                                    qs.SetQuestVarById(0, var + 1);
                                    UpdateQuestStatus(env);
                                }
                                return true;
                        }
                        break;
                }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int id = item.GetItemTemplate().GetTemplateId();
            int itemObjId = item.GetObjectId();

            if (id != 182206728)
                return HandlerResult.UNKNOWN;
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 1000, 0, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
                RemoveQuestItem(env, 182206728, 1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return ValueTask.CompletedTask;
            }, 1000L);
            return HandlerResult.SUCCESS;
        }
    }
}
