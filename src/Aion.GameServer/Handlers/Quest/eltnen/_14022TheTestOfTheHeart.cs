using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _14022TheTestOfTheHeart : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210808, 210799 };

    public _14022TheTestOfTheHeart() : base(14022)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 203900, 203996 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc in npc_ids)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        foreach (int mob_id in mob_ids)
            qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
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
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203900: // Diomedes
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 203996: // Kimeia
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
            }
        }
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203996)
            { // Kimeia
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        if (DefaultOnKillEvent(env, mob_ids, 2, 8))
        {
            QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
            if (qs.GetQuestVarById(0) == 8)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
            return true;
        }
        else
            return false;
    }
}
