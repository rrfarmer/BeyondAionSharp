using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Ritsu, Majka
/// </summary>
public class _24014StompOutThePlot : AbstractQuestHandler
{
    public _24014StompOutThePlot() : base(24014)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203665).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203668).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210562).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(216914).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203665:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            break;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 203668:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            else if (var == 5)
                                return SendQuestDialog(env, 2716);
                            break;
                        case DialogAction.SETPRO2:
                        case DialogAction.SETPRO3:
                            if (var == 1 || var == 5)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 5)
                            {
                                if (QuestService.CollectItemCheck(env, true))
                                {
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                                else
                                    return SendQuestDialog(env, 10001);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203668)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if ((targetId == 210562 || targetId == 216914) && var >= 2 && var < 5)
        {
            qs.SetQuestVarById(0, var + 1);
            UpdateQuestStatus(env);
            return true;
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24010);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24010);
    }
}
