using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _14021ToCureACurse : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210771, 210758, 210763, 210764, 210759, 210770 };

    public _14021ToCureACurse() : base(14021)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 203902, 700179, 204043, 204030 };
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
                case 203902: // Aurelius
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
                case 700179: // Paper Glider
                    if (var == 7)
                    {
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                                return SendQuestDialog(env, 2034);
                            case DialogAction.SELECT4_1:
                                ChangeQuestStep(env, 7, 8); // 7
                                return SendQuestDialog(env, 0);
                        }
                    }
                    break;
                case 204043: // Melginie
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 8)
                                return SendQuestDialog(env, 2036);
                            return false;
                        case DialogAction.SETPRO4:
                            return DefaultCloseDialog(env, 8, 9); // 8
                    }
                    break;
                case 204030: // Celestine
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 9)
                                return SendQuestDialog(env, 2376);
                            return false;
                        case DialogAction.SETPRO5:
                            return DefaultCloseDialog(env, 9, 9, true, false); // reward
                    }
                    break;
            }
        }
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203902) // Aurelius
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2716);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        switch (targetId)
        {
            case 210771:
            case 210758:
            case 210763:
            case 210764:
            case 210759:
            case 210770:
                if (var >= 1 && var <= 6)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 10)
            {
                ChangeQuestStep(env, 10, 9);
            }
        }
        return false;
    }
}
