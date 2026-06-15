using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Majka
/// </summary>
public class _1003IllegalLogging : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210096, 210149, 210145, 210146, 210150, 210151, 210092, 210160, 210154, 210685 };

    public _1003IllegalLogging() : base(1003)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203081).AddOnTalkEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int mob_id in mob_ids)
            qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 1100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 1100);
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
            if (targetId == 203081)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 7)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                    case DialogAction.SETPRO2:
                        if (var == 0 || var == 7)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
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
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs.GetStatus() != QuestStatus.START)
            return false;
        switch (targetId)
        {
            case 210096:
            case 210149:
            case 210145:
            case 210146:
            case 210150:
            case 210151:
            case 210092:
            case 210154:
            case 210685:
                if (var >= 1 && var <= 6)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
            case 210160:
                if (var == 8 || var == 9)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var == 10)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
    }
}
