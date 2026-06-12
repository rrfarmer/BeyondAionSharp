using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/ItemOrders (Altaress, Bobobear, Pad). super.onDialogEvent→base.OnDialogEvent; HandlerResult.fromBoolean→FromBoolean; DataManager/QuestService/PacketSendUtility/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class ItemOrders : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private int startItemId;
    private readonly int talkNpcId1;
    private readonly int talkNpcId2;
    private readonly int endNpcId;

    public ItemOrders(int questId, int talkNpcId1, int talkNpcId2, int endNpcId) : base(questId)
    {
        this.talkNpcId1 = talkNpcId1;
        this.talkNpcId2 = talkNpcId2;
        this.endNpcId = endNpcId;
        if (workItems == null)
        {
            log.LogWarning("Q{QuestId} has no work item", questId);
        }
        else
        {
            if (workItems.Count > 1)
                log.LogWarning("Q{QuestId} has more than 1 work item", questId);
            this.startItemId = workItems[0].GetItemId();
        }
    }

    public override void Register()
    {
        qe.RegisterQuestItem(startItemId, questId);
        if (talkNpcId1 != 0)
            qe.RegisterQuestNpc(talkNpcId1).AddOnTalkEvent(questId);
        if (talkNpcId2 != 0)
            qe.RegisterQuestNpc(talkNpcId2).AddOnTalkEvent(questId);
        if (endNpcId != 0)
            qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_ACCEPT:
                case DialogAction.QUEST_ACCEPT_1:
                case DialogAction.QUEST_ACCEPT_SIMPLE:
                    if (player.GetInventory().GetItemCountByItemId(startItemId) > 0)
                    {
                        QuestService.StartQuest(env);
                    }
                    else
                    {
                        string requiredItemL10n = DataManager.ITEM_DATA.GetItemTemplate(startItemId).GetL10n();
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_INVENTORY_ITEM(requiredItemL10n));
                    }
                    return CloseDialogWindow(env);
                default:
                    return base.OnDialogEvent(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var0 = qs.GetQuestVarById(0);
            if (targetId == talkNpcId1 || targetId == talkNpcId2)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    bool reward = ((var0 == 0 && talkNpcId2 == 0) || (var0 == 1 && talkNpcId2 != 0));
                    qs.SetQuestVarById(0, var0 + 1);
                    if (reward)
                        qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == endNpcId)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 0, 1, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == endNpcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
        }
        return HandlerResult.FAILED;
    }
}
