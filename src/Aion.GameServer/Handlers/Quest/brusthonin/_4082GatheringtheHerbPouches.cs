using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _4082GatheringtheHerbPouches : AbstractQuestHandler
{
    public _4082GatheringtheHerbPouches() : base(4082)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205190).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205190).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700430).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700431).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700432).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182209058, questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182209058 || qs == null)
            return HandlerResult.UNKNOWN;

        if (qs.GetQuestVarById(0) != 0)
            return HandlerResult.FAILED;

        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 205190)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182209058, 1))
                        return SendQuestStartDialog(env);
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    if (QuestService.CollectItemCheck(env, true))
                    {
                        RemoveQuestItem(env, 182209058, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 2716);
                }
                else
                    return SendQuestEndDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
                return SendQuestEndDialog(env);
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700430:
                case 700431:
                case 700432:
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return true;
                    break;
            }
        }
        return false;
    }
}
