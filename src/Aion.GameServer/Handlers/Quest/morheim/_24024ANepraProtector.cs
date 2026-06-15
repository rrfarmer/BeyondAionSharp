using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Ritsu
    /// </summary>
    public class _24024ANepraProtector : AbstractQuestHandler
    {
        public _24024ANepraProtector() : base(24024)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204369, 204361, 278004 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(212861).AddOnKillEvent(questId);
            qe.RegisterOnEnterZone(ZoneName.Get("ALTAR_OF_THE_BLACK_DRAGON_220020000"), questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24020);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24020);
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

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 204369:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SELECT1_1:
                                PlayQuestMovie(env, 80);
                                break;
                            case DialogAction.SETPRO1:
                                if (var == 0)
                                {
                                    return DefaultCloseDialog(env, 0, 1); // 1
                                }
                                break;
                        }
                        return false;
                    case 204361:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SETPRO2:
                                if (var == 1)
                                {
                                    return DefaultCloseDialog(env, 1, 2); // 2
                                }
                                break;
                        }
                        return false;
                    case 278004:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 1693);
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204369)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
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
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();

            if (qs.GetStatus() != QuestStatus.START)
                return false;
            switch (targetId)
            {
                case 212861:
                    if (var == 3)
                    {
                        ChangeQuestStep(env, 3, 3, true); // reward
                    }
                    break;
            }
            return false;
        }

        public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (zoneName != ZoneName.Get("ALTAR_OF_THE_BLACK_DRAGON_220020000"))
                return false;
            if (qs != null && qs.GetQuestVarById(0) == 2)
            {
                env.SetQuestId(questId);
                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                UpdateQuestStatus(env);
                PlayQuestMovie(env, 81);
                return true;
            }
            return false;
        }
    }
}
