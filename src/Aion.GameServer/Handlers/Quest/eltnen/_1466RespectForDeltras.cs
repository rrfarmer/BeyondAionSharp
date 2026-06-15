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

public class _1466RespectForDeltras : AbstractQuestHandler
{
    public _1466RespectForDeltras() : base(1466)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182201385, questId);
        qe.RegisterQuestNpc(212649).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(212649).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203903).AddOnTalkEvent(questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182201385)
            return HandlerResult.UNKNOWN;
        if (!player.IsInsideZone(ZoneName.Get("EXECUTION_GROUND_OF_DELTRAS_220020000")))
            return HandlerResult.UNKNOWN;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            player.GetInventory().DecreaseByObjectId(itemObjId, 1);
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
        if (targetId == 212649)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182201385, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203903)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
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
