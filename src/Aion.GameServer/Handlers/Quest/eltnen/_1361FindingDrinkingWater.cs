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

public class _1361FindingDrinkingWater : AbstractQuestHandler
{
    public _1361FindingDrinkingWater() : base(1361)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182201326, questId); // Empty Bucket
        qe.RegisterQuestNpc(203943).AddOnQuestStart(questId); // Turiel start
        qe.RegisterQuestNpc(203943).AddOnTalkEvent(questId); // Turiel talk
        qe.RegisterQuestNpc(700173).AddOnTalkEvent(questId); // Water tank
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182201326) // Empty Bucket
            return HandlerResult.UNKNOWN;
        if (!player.IsInsideItemUseZone(ZoneName.Get("LF2_ITEMUSEAREA_Q1361")))
            return HandlerResult.UNKNOWN;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            player.GetInventory().DecreaseByObjectId(itemObjId, 1);
            GiveQuestItem(env, 182201327, 1);
            qs.SetQuestVar(1);
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
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203943) // Turiel
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182201326, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD) // Reward
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
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
        else if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
        {
            switch (targetId)
            {
                case 700173: // Water Tank
                    if (qs.GetQuestVarById(0) == 1 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return UseQuestObject(env, 1, 1, true, 0, 0, 0, 182201327, 1); // reward
                    }
                    break;
            }
        }
        return false;
    }
}
