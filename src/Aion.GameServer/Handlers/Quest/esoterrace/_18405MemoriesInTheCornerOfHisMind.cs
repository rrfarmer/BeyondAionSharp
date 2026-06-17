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
/// @author Vincas
/// </summary>
public class _18405MemoriesInTheCornerOfHisMind : AbstractQuestHandler
{
    private const int npcDaidra = 799553, npcTillen = 799552;

    public _18405MemoriesInTheCornerOfHisMind() : base(18405)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcDaidra).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(npcTillen).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182215002, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (env.GetTargetId() == 0 && env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
        {
            QuestService.StartQuest(env);
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
            return true;
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (env.GetTargetId())
            {
                case npcDaidra:
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, 0, 1, 182215024, 1, 182215002, 1);
                    }
                    return false;
                case npcTillen:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                            RemoveQuestItem(env, 182215024, 1);
                        return DefaultCloseDialog(env, 1, 2, true, true);
                    }
                    break;
            }
        }
        return SendQuestRewardDialog(env, npcTillen, 0);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182215002)
            return HandlerResult.FAILED;
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            SendQuestDialog(env, 4);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }
}
