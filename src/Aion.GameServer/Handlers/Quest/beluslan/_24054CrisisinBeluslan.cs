using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Ritsu
    /// </summary>
    public class _24054CrisisinBeluslan : AbstractQuestHandler
    {
        public _24054CrisisinBeluslan() : base(24054)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204701, 204702, 802053 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(702041).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(233865).AddOnKillEvent(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            int[] quests = { 24053, 24052, 24051, 24050 };
            DefaultOnQuestCompletedEvent(env, quests);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            int[] quests = { 24053, 24052, 24051, 24050 };
            DefaultOnLevelChangedEvent(player, quests);
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            switch (env.GetTargetId())
            {
                case 702041:
                    if (qs.GetQuestVarById(0) >= 2 && qs.GetQuestVarById(0) < 5)
                    {
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                    }
                    break;
                case 233865:
                    if (qs.GetQuestVarById(0) == 5)
                    {
                        ChangeQuestStep(env, 5, 6); // 6
                    }
                    break;
            }
            return false;
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

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204702) // Nerita
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 204702) // Nerita
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        break;
                    case DialogAction.SELECT1_2:
                        PlayQuestMovie(env, 255);
                        break;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                            return DefaultCloseDialog(env, 0, 1); // 1
                        break;
                }
            }
            else if (targetId == 802053) // Fafner
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        break;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                            return DefaultCloseDialog(env, 1, 2); // 2
                        break;
                }
            }
            else if (targetId == 204701) // Hod
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 6)
                            return SendQuestDialog(env, 2375);
                        break;
                    case DialogAction.SET_SUCCEED:
                        if (var == 6)
                            return DefaultCloseDialog(env, 6, 6, true, false); // reward
                        break;
                }
            }
            return false;
        }
    }
}
