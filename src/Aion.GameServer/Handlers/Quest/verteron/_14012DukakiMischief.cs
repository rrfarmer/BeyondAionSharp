using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Majka
/// </summary>
public class _14012DukakiMischief : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210145, 210146, 210157 };

    public _14012DukakiMischief() : base(14012)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 203129, 203098 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int mob_id in mob_ids)
            qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
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
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203098)
                return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 203129)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    else if (var == 1)
                        return SendQuestDialog(env, 1693);
                    return false;
                case DialogAction.SETPRO1:
                    if (var == 0)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
                case DialogAction.SETPRO3:
                    int var1 = qs.GetQuestVarById(1);
                    int var2 = qs.GetQuestVarById(2);
                    if (var1 == 5 && var2 == 3)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            if (var == 1)
            {
                int[] mobs = { 210145, 210146 };
                if (targetId == 210145 || targetId == 210146)
                    return DefaultOnKillEvent(env, mobs, 0, 5, 1);
                else if (targetId == 210157)
                    return DefaultOnKillEvent(env, 210157, 0, 3, 2);
            }
        }
        return false;
    }
}
