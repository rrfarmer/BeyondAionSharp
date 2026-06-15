using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _14013AFrillOfAFuss : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210126, 210201, 210202, 210200 };

    public _14013AFrillOfAFuss() : base(14013)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int mob_id in mob_ids)
            qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(203129).AddOnTalkEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 14010);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 14010);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
        {
            targetId = npc.GetNpcId();
        }

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203129)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        else if (var == 1)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        return false;
                    case DialogAction.SELECT1_1:
                        PlayQuestMovie(env, 27);
                        return SendQuestDialog(env, 1012);
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203129)
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
        int targetId = env.GetTargetId();
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 1)
            {
                int var1 = qs.GetQuestVarById(1);
                int var2 = qs.GetQuestVarById(2);
                int[] mobs = { 210200, 210201 };
                if (targetId == 210126)
                    return DefaultOnKillEvent(env, 210126, 0, 5, 1);
                else if (targetId == 210200 || targetId == 210201)
                    return DefaultOnKillEvent(env, mobs, 0, 7, 2);
                else if (var1 == 5 && var2 == 7)
                    return DefaultOnKillEvent(env, 210202, 0, true, 3);
            }
        }
        return false;
    }
}
