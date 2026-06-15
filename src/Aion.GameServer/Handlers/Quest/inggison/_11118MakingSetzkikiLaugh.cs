using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Luzien, Neon
/// </summary>
public class _11118MakingSetzkikiLaugh : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 798985, 798963, 798986 };

    public _11118MakingSetzkikiLaugh() : base(11118)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798985).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
        {
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestItem(182206795, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 798985, 4762))
            return true;
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int targetId = env.GetTargetId();
        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798963)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 1)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 1, 2, false, 10000, 10001, 182206795, 1);
                }
            }
            else if (targetId == 798986)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SET_SUCCEED:
                        return DefaultCloseDialog(env, 3, 4, true, false);
                }
            }
        }
        return SendQuestRewardDialog(env, 798985, 10002);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 2)
            {
                if (player.IsInsideItemUseZone(item.GetItemTemplate().GetUseArea()))
                {
                    return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 2, 3, false));
                }
            }
        }
        return HandlerResult.FAILED;
    }
}
