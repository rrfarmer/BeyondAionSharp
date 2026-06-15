using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Ritsu, Majka
/// </summary>
public class _24012AnOminousCrop : AbstractQuestHandler
{
    public _24012AnOminousCrop() : base(24012)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203605).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700096).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("MUMU_FARMLAND_220030000"), questId);
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
                case 203605: // Loriniah
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            else if (var == 5)
                                return SendQuestDialog(env, 2716);
                            return false;
                        case DialogAction.SELECT1_1_1:
                            PlayQuestMovie(env, 61);
                            return SendQuestDialog(env, 1013);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 5, 6, true, 5, 10001); // reward
                    }
                    break;
                case 700096: // MuMu Cart
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var >= 2 && var < 5)
                            {
                                return UseQuestObject(env, var, var + 1, false, true); // 4,5
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203605) // Loriniah
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName == ZoneName.Get("MUMU_FARMLAND_220030000"))
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 1)
                {
                    ChangeQuestStep(env, 1, 2); // 2
                    return true;
                }
            }
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
