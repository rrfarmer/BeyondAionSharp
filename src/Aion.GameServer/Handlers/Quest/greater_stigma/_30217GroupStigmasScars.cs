using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _30217GroupStigmasScars : AbstractQuestHandler
{
    public _30217GroupStigmasScars() : base(30217)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798909).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798909).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799506).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798941).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798909)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 798941:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            SpawnForFiveMinutesInFrontOf(799506, player, 1.5f);
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 798909:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182209618);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(182209619);
                            if (var == 2)
                            {
                                if (itemCount1 > 0 && itemCount2 > 0)
                                {
                                    RemoveQuestItem(env, 182209618, 1);
                                    RemoveQuestItem(env, 182209619, 1);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 1693);
                                }
                                else
                                    return SendQuestDialog(env, 10001);
                            }
                            break;
                    }
                    return false;
                case 799506:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO2:
                            env.GetVisibleObject().GetController().Delete();
                            qs.SetQuestVarById(0, 2);
                            UpdateQuestStatus(env);
                            return true;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798909)
            {
                if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
