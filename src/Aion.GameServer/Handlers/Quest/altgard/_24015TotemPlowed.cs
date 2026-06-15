using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _24015TotemPlowed : AbstractQuestHandler
{
    public _24015TotemPlowed() : base(24015)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203669).AddOnTalkEvent(questId); // Taora
        qe.RegisterQuestNpc(203557).AddOnTalkEvent(questId); // Suthran
        qe.RegisterQuestNpc(700099).AddOnKillEvent(questId); // Zemurru's Totem
        qe.RegisterOnEnterZone(ZoneName.Get("DF1A_SENSORYAREA_Q2021_206013_2_220030000"), questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }
        int var = qs.GetQuestVarById(0);
        if (var >= 2 && var < 4)
        {
            qs.SetQuestVarById(0, var + 1);
            UpdateQuestStatus(env);
            ((Npc)env.GetVisibleObject()).GetController().Delete();
            return true;
        }
        else if (var == 4)
        {
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            ((Npc)env.GetVisibleObject()).GetController().Delete();
            return true;
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203669: // Taora
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SELECT1_1_1:
                            PlayQuestMovie(env, 218);
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 1013);
                        case DialogAction.SETPRO1:
                            return CloseDialogWindow(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203557) // Suthran
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get("DF1A_SENSORYAREA_Q2021_206013_2_220030000"))
            return false;
        Player player = env.GetPlayer();
        if (player == null)
            return false;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        if (qs.GetQuestVarById(0) == 1)
        {
            qs.SetQuestVarById(0, 2);
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
