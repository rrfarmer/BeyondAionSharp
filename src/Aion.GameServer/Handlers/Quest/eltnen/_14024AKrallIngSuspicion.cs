using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur
/// </summary>
public class _14024AKrallIngSuspicion : AbstractQuestHandler
{
    public _14024AKrallIngSuspicion() : base(14024)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 203904, 204045, 204003, 204004, 204020, 203901 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 14020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 14020);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204020)
            {
                RemoveQuestItem(env, 182201004, 1);
                qs.SetRewardGroup(0); // group 0 and 1 are identical in templates, set anyway to mute warning
                return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 203904)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    return false;
                case DialogAction.SETPRO1:
                    if (var == 0)
                    {
                        return DefaultCloseDialog(env, 0, 1);
                    }
                    break;
            }
        }
        else if (targetId == 204045)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 1)
                        return SendQuestDialog(env, 1352);
                    return false;
                case DialogAction.SELECT2_1_1:
                    if (var == 1)
                        PlayQuestMovie(env, 32);
                    break;
                case DialogAction.SETPRO2:
                    if (var == 1)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        TeleportService.TeleportTo(player, 210020000, 1357f, 2566f, 279.6f, (byte)89, TeleportAnimation.FADE_OUT_BEAM);
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 204003)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 2)
                        return SendQuestDialog(env, 1693);
                    else if (var == 3 && QuestService.CollectItemCheck(env, true))
                        return SendQuestDialog(env, 2034);
                    else
                        return SendQuestDialog(env, 2120);
                case DialogAction.SETPRO3:
                    if (var == 2)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
                case DialogAction.SETPRO4:
                    if (var == 3)
                    {
                        PlayQuestMovie(env, 50);
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 204004)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 2)
                        return SendQuestDialog(env, 2034);
                    break;
                case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                    if (var == 2)
                        return CheckQuestItems(env, 2, 2, false, 2802, 2717);
                    return false;
                case DialogAction.SETPRO4:
                    if (var == 2)
                    {
                        if (!GiveQuestItem(env, 182201004, 1))
                            return true;
                        ChangeQuestStep(env, 2, 2, true);
                        TeleportService.TeleportTo(player, 210020000, 1608.11f, 1528.7f, 318.07f, (byte)118, TeleportAnimation.FADE_OUT_BEAM);
                        return true;
                    }
                    break;
            }
        }
        return false;
    }
}
