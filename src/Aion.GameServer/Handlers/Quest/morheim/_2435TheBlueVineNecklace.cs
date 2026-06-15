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

/// <summary>
/// @author MrPoke, Nephis
/// </summary>
public class _2435TheBlueVineNecklace : AbstractQuestHandler
{
    public _2435TheBlueVineNecklace() : base(2435)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204387).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204390).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182204181, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0)
            {
                if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204387)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 204390)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 1, 1, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204390)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                RemoveQuestItem(env, 182204181, 1);
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

        if (id != 182204181)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            SendQuestDialog(env, 4);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }
}
