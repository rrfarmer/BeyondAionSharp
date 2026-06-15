using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _29064FangOfConstruction : AbstractQuestHandler
{
    public _29064FangOfConstruction() : base(29064)
    {
    }

    public override void Register()
    {
        int[] npcs = { 798452, 204075, 204053 };
        qe.RegisterQuestNpc(204053).AddOnQuestStart(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204053)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798452:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 152209288, 1, 0, 0);
                    }
                    break;
                case 204075:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1 && player.GetInventory().GetItemCountByItemId(182213239) >= 1
                                && player.GetInventory().GetItemCountByItemId(186000085) >= 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM_SIMPLE:
                            if (player.GetInventory().GetItemCountByItemId(182213239) >= 1 && player.GetInventory().GetItemCountByItemId(186000085) >= 1)
                            {
                                RemoveQuestItem(env, 182213239, 1);
                                RemoveQuestItem(env, 186000085, 1);
                                return DefaultCloseDialog(env, 1, 1, true, false);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204075)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
