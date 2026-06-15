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
/// @author Mr. Poke
/// </summary>
public class _2114TheInsectProblem : AbstractQuestHandler
{
    public _2114TheInsectProblem() : base(2114)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203533).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203533).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210734).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210380).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210381).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203533)
        {
            if (qs == null || qs.IsStartable())
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        if (QuestService.StartQuest(env))
                        {
                            qs = player.GetQuestStateList().GetQuestState(questId);
                            qs.SetQuestVar(1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        if (QuestService.StartQuest(env))
                        {
                            qs = player.GetQuestStateList().GetQuestState(questId);
                            qs.SetQuestVar(11);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs.GetStatus() != QuestStatus.START)
            return false;
        switch (targetId)
        {
            case 210734:
                if (var >= 1 && var < 10)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var == 10)
                {
                    qs.SetRewardGroup(0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
                return false;
            case 210380:
            case 210381:
                if (var >= 11 && var < 20)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var == 20)
                {
                    qs.SetRewardGroup(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return CloseDialogWindow(env);
                }
                break;
        }
        return false;
    }
}
