using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Gigi
    /// </summary>
    public class _30203GroupHalttheCeremony : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 798926 };

        public _30203GroupHalttheCeremony() : base(30203)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(798926).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(216175).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(216177).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(216179).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(216181).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(216182).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(216263).AddOnKillEvent(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 798926)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 798926)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 10002);
                    }
                }
                return false;
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798926)
                    return SendQuestEndDialog(env);
                return false;
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            int var = qs.GetQuestVarById(0);
            int var1 = qs.GetQuestVarById(1);
            int var2 = qs.GetQuestVarById(2);
            int var3 = qs.GetQuestVarById(3);

            switch (targetId)
            {
                case 216175:
                    if (var == 0 || var1 == 0 || var2 == 1 || var3 == 1 || var1 == 0 || var2 == 0 || var3 == 0)
                    {
                        qs.SetQuestVarById(0, 1);
                        UpdateQuestStatus(env);
                    }
                    break;
                case 216177:
                    if (var == 1 || var1 == 0 || var2 == 1 || var3 == 1 || var == 0 || var2 == 0 || var3 == 0)
                    {
                        qs.SetQuestVarById(1, 1);
                        UpdateQuestStatus(env);
                    }
                    break;
                case 216179:
                    if (var == 1 || var1 == 0 || var2 == 0 || var3 == 1 || var == 0 || var1 == 0 || var3 == 0)
                    {
                        qs.SetQuestVarById(2, 1);
                        UpdateQuestStatus(env);
                    }
                    break;
                case 216181:
                    if (var == 1 || var1 == 0 || var2 == 1 || var3 == 0 || var == 0 || var2 == 0 || var1 == 0)
                    {
                        qs.SetQuestVarById(3, 1);
                        UpdateQuestStatus(env);
                    }
                    break;
                case 216182:
                case 216263:
                    if (var == 1 && var1 == 1 && var2 == 1 && var3 == 1)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        PlayQuestMovie(env, 443);
                    }
                    break;
            }
            return false;
        }
    }
}
