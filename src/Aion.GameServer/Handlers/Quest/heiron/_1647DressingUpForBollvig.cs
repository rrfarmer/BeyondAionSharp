using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar, vlog
/// </summary>
public class _1647DressingUpForBollvig : AbstractQuestHandler
{
    public _1647DressingUpForBollvig() : base(1647)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(790019).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(790019).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700272).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 790019) // Zetus
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    default:
                    {
                        return SendQuestStartDialog(env, 182201783, 1);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700272) // Suspicious Stone Statue
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    // Wearing Stenon Blouse and Stenon Skirt
                    if (player.GetEquipment().GetEquippedItemsByItemId(110100150).Count != 0
                        && player.GetEquipment().GetEquippedItemsByItemId(113100144).Count != 0)
                    {
                        // Having Myanee's Flute
                        if (player.GetInventory().GetItemCountByItemId(182201783) > 0)
                        {
                            PlayQuestMovie(env, 199);
                            return UseQuestObject(env, 0, 0, true, false); // reward
                        }
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790019) // Zetus
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 10002);
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 199)
        {
            Player player = env.GetPlayer();
            SpawnForFiveMinutesInFrontOf(204635, player, 2);
            SpawnForFiveMinutes(204635, player.GetWorldMapInstance(), player.GetX() + 2, player.GetY() - 2, player.GetZ(), (byte)0);
            SpawnForFiveMinutes(204635, player.GetWorldMapInstance(), player.GetX() - 2, player.GetY() + 2, player.GetZ(), (byte)0);
        }
    }
}
