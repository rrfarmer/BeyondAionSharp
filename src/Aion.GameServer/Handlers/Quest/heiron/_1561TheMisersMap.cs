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
/// Find the place marked on the map (182201728).
///
/// @author Balthazar, vlog
/// </summary>
public class _1561TheMisersMap : AbstractQuestHandler
{
    public _1561TheMisersMap() : base(1561)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(700188).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182201728, questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        env.SetQuestId(questId);
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
                RemoveQuestItem(env, id, 1);
                QuestService.StartQuest(env);
                // SendQuestDialog(env, 4);
                return ValueTask.CompletedTask;
            }, 3000L);
            return HandlerResult.SUCCESS;
        }
        return HandlerResult.UNKNOWN;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700188) // Jewel Box
            {
                if (var == 0)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT || env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    {
                        return DefaultCloseDialog(env, 0, 0, true, true);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 700188) // Jewel Box
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
