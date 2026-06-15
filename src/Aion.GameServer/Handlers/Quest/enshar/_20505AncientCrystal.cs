using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _20505AncientCrystal : AbstractQuestHandler
{
    public _20505AncientCrystal() : base(20505)
    {
    }

    public override void Register()
    {
        // Cenute 804732
        // Malite 804733
        int[] npcs = { 804732, 804733, 804734, 804735 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(219953).AddOnKillEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
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

        switch (targetId)
        {
            case 804732:
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Cenute.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }

                if (qs.GetStatus() == QuestStatus.REWARD)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }

                    return SendQuestEndDialog(env);
                }
                break;
            case 804733:
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Talk with Malite.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 804734:
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 2) // Step 2: Investigate the Field Wardens Bodyguard Corpse nearby.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);

                        if (dialogActionId == DialogAction.SETPRO3)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 804735:
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 3) // Step 3: Investigate the Field Warden Corpse.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2034);

                        if (dialogActionId == DialogAction.SETPRO4)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
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
        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 219953:
                if (var == 4) // Step 4: Track down and kill the Beritra Research Corps Warmage in Timeswept Altar.
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20500);
    }
}
