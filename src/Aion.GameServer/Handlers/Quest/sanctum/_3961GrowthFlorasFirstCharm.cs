using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Talk with Erdos (203740). Prepare the money (40000) for the charm, and bring it to Flora (798384).
///
/// @author undertrey, vlog
/// </summary>
public class _3961GrowthFlorasFirstCharm : AbstractQuestHandler
{
    public _3961GrowthFlorasFirstCharm() : base(3961)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798384).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798384).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203740).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798384)
            { // Flora
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env, 182206108, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 203740: // Erdos
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 0, 0, 182206108, 1); // 1
                    }
                    return false;
                case 798384: // Flora
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                RemoveQuestItem(env, 182206108, 1);
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 1 && player.GetInventory().TryDecreaseKinah(40000))
                            {
                                ChangeQuestStep(env, 1, 1, true); // reward
                                return SendQuestDialog(env, 5);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 1, 1);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798384)
            { // Flora
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
