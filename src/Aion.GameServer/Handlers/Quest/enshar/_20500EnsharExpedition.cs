using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Go to Enshar and talk with Cogelhogan.
/// Talk with Haldor.
/// Order: Go to Enshar and meet with Cogelhogan.
///
/// @author Majka
/// </summary>
public class _20500EnsharExpedition : AbstractQuestHandler
{
    public _20500EnsharExpedition() : base(20500)
    {
    }

    public override void Register()
    {
        // Cogelhogan 804718
        // Haldor 804719
        int[] npcs = { 804718, 804719 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnEnterWorld(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        switch (targetId)
        {
            case 804718: // Cogelhogan
                if (qs.GetStatus() == QuestStatus.START) // Step 0: Go to Enshar and talk with Cogelhogan.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 1011);
                    }

                    if (dialogActionId == DialogAction.SET_SUCCEED)
                    {
                        qs.SetQuestVar(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                    }
                }
                break;
            case 804719: // Haldor
                if (qs.GetStatus() == QuestStatus.REWARD) // Step 1: Talk with Haldor.
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    return SendQuestEndDialog(env);
                }
                break;
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        if (player.GetWorldId() == WorldMapType.ENSHAR.GetId() && !player.GetQuestStateList().HasQuest(questId))
            return QuestService.StartQuest(env);
        return false;
    }

    public override void OnLevelChangedEvent(Player player)
    {
        OnEnterWorldEvent(new QuestEnv(null, player, questId));
    }
}
