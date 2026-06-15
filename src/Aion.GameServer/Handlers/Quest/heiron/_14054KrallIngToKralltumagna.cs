using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Ritsu, Majka
    /// </summary>
    public class _14054KrallIngToKralltumagna : AbstractQuestHandler
    {
        private static readonly int[] indratu_runaway = { 235483, 235484, 235485, 235486, 235487, 235488, 235489, 235490, 235491, 235492, 235493, 235494,
            235495, 235496, 235497, 235498, 235499, 235500, 235501, 235502 };
        private static readonly int baranath = 702040;
        private static readonly int vitusa = 233861;

        public _14054KrallIngToKralltumagna() : base(14054)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204602, 800413, 802050 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLogOut(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc in npc_ids)
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            foreach (int mob_id in indratu_runaway)
                qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(baranath).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(vitusa).AddOnKillEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14050, 14051, 14052, 14053);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14050, 14051, 14052, 14053);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int targetId = env.GetTargetId();
            int var = qs.GetQuestVarById(0);
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 204602: // Atalante
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                break;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                        break;
                    case 800413: // Javlantia
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                break;
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, var, var + 1); // 2
                        }
                        break;

                    case 802050: // Bartyn
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 2034);
                                if (var == 4)
                                    return SendQuestDialog(env, 2716);
                                break;
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 2, 3); // 3
                            case DialogAction.SETPRO6:
                                qs.SetRewardGroup(0); // both available groups are the same
                                return DefaultCloseDialog(env, 4, 5, true, false); // 3
                        }
                        break;
                }
            }
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 800413)
                { // Javlantia
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 3057);
                    return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (qs.GetQuestVarById(0) == 2)
                {
                    return DefaultOnKillEvent(env, indratu_runaway, 0, 6, 1) || DefaultOnKillEvent(env, baranath, 0, 3, 2);
                }
                if (qs.GetQuestVarById(0) == 3)
                {
                    return DefaultOnKillEvent(env, vitusa, 3, 4); // 4
                }
            }
            return false;
        }
    }
}
