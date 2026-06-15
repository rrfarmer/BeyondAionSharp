using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Rolandas
    /// </summary>
    public class _80033EventAvertingTheGaze : AbstractQuestHandler
    {
        public _80033EventAvertingTheGaze() : base(80033)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(799781).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(799781).AddOnTalkEvent(questId);
            qe.RegisterQuestItem(188051133, questId); // [Event] Charm Card
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            int targetId = env.GetTargetId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.IsStartable())
                return false;

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 799781)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1011);
                    else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    {
                        Storage storage = player.GetInventory();
                        if (storage.GetItemCountByItemId(164002015) > 0)
                            return SendQuestDialog(env, 2375);
                        else
                            return SendQuestDialog(env, 2716);
                    }
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    {
                        if (qs.GetQuestVarById(0) == 0)
                            DefaultCloseDialog(env, 0, 1, true, true, 0, 0, 164002015, 1);
                        return SendQuestDialog(env, 5);
                    }
                    else if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                        return SendQuestRewardDialog(env, 799781, 5);
                    else
                        return SendQuestStartDialog(env);
                }
            }
            return SendQuestRewardDialog(env, 799781, 0);
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            // check if the parent quest is active (you get Charm Cards)
            if (!EventService.GetInstance().IsActiveEventQuest(80032))
                return HandlerResult.FAILED;

            Player player = env.GetPlayer();

            // the same item registered for elyos quests, return UNKNOWN for them
            // to not start asmodians quests
            if (item.GetItemTemplate().GetTemplateId() == 188051133 && player.GetCommonData().GetRace().Equals(Race.ASMODIANS))
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    Storage storage = player.GetInventory();
                    QuestStatus status;

                    if (storage.GetItemCountByItemId(164002015) > 0)
                    {
                        status = GetQuestUpdateStatus(player, questId);
                        // got a Beritra's Gaze, then start me
                        QuestService.StartEventQuest(new QuestEnv(null, player, questId), status);
                    }
                    if (storage.GetItemCountByItemId(164002016) > 9) // Israphel's Glory
                    {
                        status = GetQuestUpdateStatus(player, 80037);
                        QuestService.StartEventQuest(new QuestEnv(null, player, 80037), status);
                    }
                    if (storage.GetItemCountByItemId(164002017) > 4) // Siel's Gift
                    {
                        status = GetQuestUpdateStatus(player, 80038);
                        QuestService.StartEventQuest(new QuestEnv(null, player, 80038), status);
                    }
                    if (storage.GetItemCountByItemId(164002018) > 0) // Aion's Grace
                    {
                        status = GetQuestUpdateStatus(player, 80039);
                        QuestService.StartEventQuest(new QuestEnv(null, player, 80039), status);
                    }
                    return ValueTask.CompletedTask;
                }, 10000L);
                return HandlerResult.SUCCESS;
            }
            return HandlerResult.UNKNOWN;
        }

        private QuestStatus GetQuestUpdateStatus(Player player, int questid)
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(questid);
            QuestStatus status = qs == null ? QuestStatus.START : qs.GetStatus();

            if (qs != null && questid != questId && status == QuestStatus.COMPLETE)
                status = QuestStatus.START;
            return status;
        }
    }
}
