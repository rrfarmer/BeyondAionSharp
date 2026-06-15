using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _14016AGateAgape : AbstractQuestHandler
{
    public _14016AGateAgape() : base(14016)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203098, 700142 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterOnDie(questId);
        qe.RegisterQuestNpc(233873).AddOnKillEvent(questId);
        foreach (int npcId in npcs)
            qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
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
                case 203098: // Spatalos
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            TeleportService.TeleportTo(player, 210030000, 2683.2085f, 1068.8977f, 199.375f);
                            ChangeQuestStep(env, 0, 1); // 1
                            return CloseDialogWindow(env);
                    }
                    break;
                case 700142: // Abyss Gate Guardian Stone
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (QuestService.CollectItemCheck(env, true))
                        {
                            return PlayQuestMovie(env, 153);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203098) // Spatalos
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2034);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVars().GetQuestVars();
            if (var == 2)
            {
                ChangeQuestStep(env, 2, 1);
                RemoveQuestItem(env, 182215317, 1);
                return true;
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVars().GetQuestVars();
            if (var == 2 && player.GetWorldId() != 310030000)
            {
                ChangeQuestStep(env, 2, 1);
                RemoveQuestItem(env, 182215317, 1);
                return true;
            }
            else if (var == 1 && player.GetWorldId() == 310030000)
            {
                ChangeQuestStep(env, 1, 2); // 2
                SpawnForFiveMinutes(233873, player.GetWorldMapInstance(), (float)258.89917, (float)237.20166, (float)217.06035, (byte)0);
                return true;
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
            if (var == 2)
            {
                if (env.GetTargetId() == 233873)
                {
                    if (player.GetInventory().GetItemCountByItemId(182215317) < 1)
                    {
                        return GiveQuestItem(env, 182215317, 1);
                    }
                }
            }
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId != 153)
            return;
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            qs.SetRewardGroup(0);
            ChangeQuestStep(env, 2, 2, true); // reward
            TeleportService.TeleportTo(player, 210030000, 2683.2085f, 1068.8977f, 199.375f);
        }
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        int[] verteronQuests = { 14015, 14014, 14013, 14012, 14011, 14010 };
        DefaultOnQuestCompletedEvent(env, verteronQuests);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        int[] verteronQuests = { 14015, 14014, 14013, 14012, 14011, 14010 };
        DefaultOnLevelChangedEvent(player, verteronQuests);
    }
}
