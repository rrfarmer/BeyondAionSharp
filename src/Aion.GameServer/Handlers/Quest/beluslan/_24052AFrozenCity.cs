using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Ritsu, Majka
    /// </summary>
    public class _24052AFrozenCity : AbstractQuestHandler
    {
        public _24052AFrozenCity() : base(24052)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204753, 790016, 730036, 279000 };
            qe.RegisterQuestItem(182215378, questId);
            qe.RegisterQuestItem(182215379, questId);
            qe.RegisterQuestItem(182215380, questId);
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24050);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24050);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204753)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    int[] questItems = { 182215378, 182215379, 182215380 };
                    return SendQuestEndDialog(env, questItems);
                }
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 204753)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SELECT1_1:
                        PlayQuestMovie(env, 242);
                        break;
                    case DialogAction.SELECT1_2:
                        if (var == 0 && player.GetInventory().GetItemCountByItemId(182215378) != 1)
                        {
                            if (GiveQuestItem(env, 182215378, 1))
                                return SendQuestDialog(env, 1097);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            int id = item.GetItemTemplate().GetTemplateId();
            int itemObjId = item.GetObjectId();

            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (!player.IsInsideItemUseZone(ZoneName.Get("DF3_ITEMUSEAREA_Q2056")))
                return HandlerResult.FAILED;

            if (id != 182215378 && qs.GetQuestVarById(0) == 1 || id != 182215379 && qs.GetQuestVarById(0) == 2
                || id != 182215380 && qs.GetQuestVarById(0) == 3)
                return HandlerResult.UNKNOWN;

            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 2000, 0, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
                if (qs.GetQuestVarById(0) == 1)
                {
                    PlayQuestMovie(env, 243);
                    RemoveQuestItem(env, id, 1);
                    ChangeQuestStep(env, 1, 2); // 2
                    GiveQuestItem(env, 182215379, 1);
                }
                else if (qs.GetQuestVarById(0) == 2)
                {
                    PlayQuestMovie(env, 244);
                    RemoveQuestItem(env, id, 1);
                    ChangeQuestStep(env, 2, 3); // 3
                    GiveQuestItem(env, 182215380, 1);
                }
                else if (qs.GetQuestVarById(0) == 3 && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    RemoveQuestItem(env, id, 1);
                    PlayQuestMovie(env, 245);
                    qs.SetQuestVar(4);
                    ChangeQuestStep(env, 4, 4, true); // reward
                }
                return ValueTask.CompletedTask;
            }, 2000L);
            return HandlerResult.SUCCESS;
        }
    }
}
