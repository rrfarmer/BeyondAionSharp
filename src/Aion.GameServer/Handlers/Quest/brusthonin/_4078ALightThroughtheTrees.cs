using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Leunam, zhkchi
    /// </summary>
    public class _4078ALightThroughtheTrees : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 205157, 700427, 700428, 700429 };

        public _4078ALightThroughtheTrees() : base(4078)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(205157).AddOnQuestStart(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();

            if (targetId == 205157)
            {
                if (qs == null || qs.IsStartable())
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 205157)
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 10002);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }

            if (targetId == 205157)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (player.GetInventory().GetItemCountByItemId(182209049) >= 9)
                        {
                            if (!GiveQuestItem(env, 182209050, 1))
                                return true;
                            RemoveQuestItem(env, 182209049, 9);
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 10000);
                        }
                        else
                            return SendQuestDialog(env, 10001);
                }
            }
            else if (targetId == 700428)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 1)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182209050) == 1)
                            {
                                return UseQuestObject(env, 1, 2, false, false); // 1
                            }
                        }
                        return false;
                }
            }
            else if (targetId == 700427)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 2)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182209050) == 1)
                            {
                                return UseQuestObject(env, 2, 3, false, false); // 2
                            }
                        }
                        return false;
                }
            }
            else if (targetId == 700429)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 3)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182209050) == 1)
                            {
                                return UseQuestObject(env, 3, 4, true, false); // 3
                            }
                        }
                        return false;
                }
            }
            return false;
        }
    }
}
