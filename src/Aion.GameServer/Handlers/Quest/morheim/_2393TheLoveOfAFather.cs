using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nephis and AU quest helper Team
/// </summary>
public class _2393TheLoveOfAFather : AbstractQuestHandler
{
    public _2393TheLoveOfAFather() : base(2393)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182204162, questId);
        qe.RegisterQuestNpc(204343).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204343).AddOnTalkEvent(questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182204162)
            return HandlerResult.UNKNOWN;
        if (!player.IsInsideItemUseZone(ZoneName.Get("DF2_ITEMUSEAREA_Q2393")))
            return HandlerResult.UNKNOWN;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            player.GetInventory().DecreaseByObjectId(itemObjId, 1);
            GiveQuestItem(env, 182204163, 1);
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
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
        if (targetId == 204343)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182204162, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.REWARD)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetQuestVar(2);
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
}
