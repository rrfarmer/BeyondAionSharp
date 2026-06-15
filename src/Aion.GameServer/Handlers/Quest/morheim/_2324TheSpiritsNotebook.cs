using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis
/// </summary>
public class _2324TheSpiritsNotebook : AbstractQuestHandler
{
    public _2324TheSpiritsNotebook() : base(2324)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204373).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182204123, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null)
            return false;

        if (targetId == 0)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                qs.SetStatus(QuestStatus.START);
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                return true;
            }
        }
        else if (targetId == 204373)
        {
            if (qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    RemoveQuestItem(env, 182204123, 1);
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

        if (id != 182204123)
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
