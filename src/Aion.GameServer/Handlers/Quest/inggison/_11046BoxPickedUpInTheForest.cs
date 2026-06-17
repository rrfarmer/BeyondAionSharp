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
/// @author Dta3000
/// </summary>
public class _11046BoxPickedUpInTheForest : AbstractQuestHandler
{
    public _11046BoxPickedUpInTheForest() : base(11046)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798954).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182206745, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();
        if (targetId == 0)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_ACCEPT_1:
                    QuestService.StartQuest(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                    return true;
                case DialogAction.QUEST_REFUSE_1:
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                    return true;
            }
        }
        else if (targetId == 798954)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    RemoveQuestItem(env, 182206745, 1);
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
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (id != 182206745)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 20, 1, 0), true);
        if (qs == null || qs.IsStartable())
            SendQuestDialog(env, 4);
        return HandlerResult.SUCCESS;
    }
}
