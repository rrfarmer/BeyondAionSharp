using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14051RootOfTheProblem : AbstractQuestHandler
    {
        public _14051RootOfTheProblem() : base(14051)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204500, 204549, 730026, 730024 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestItem(182215337, questId);
            qe.RegisterQuestItem(182215338, questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14050);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14050);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.REWARD)
            { // Trajanus
                if (targetId == 730024)
                {
                    RemoveQuestItem(env, 182215337, 3);
                    RemoveQuestItem(env, 182215338, 3);
                    return SendQuestEndDialog(env);
                }
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 204500)
            { // Perento
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 204549)
            { // Aphesius
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        else if (var == 2)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SELECT2_1:
                        return SendQuestDialog(env, 1353);
                    case DialogAction.SELECT2_1_1:
                        return SendQuestDialog(env, 1354);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2); // 2
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (QuestService.CollectItemCheck(env, true))
                        {
                            ChangeQuestStep(env, 2, 3);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 10000); // 3
                        }
                        else
                            return SendQuestDialog(env, 10001);
                }
            }
            else if (targetId == 730026)
            {// Mersephon
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SELECT4_1:
                        return SendQuestDialog(env, 2035);
                    case DialogAction.SETPRO4:
                        return DefaultCloseDialog(env, 3, 4, true, false); // 4
                }
            }
            return false;
        }
    }
}
