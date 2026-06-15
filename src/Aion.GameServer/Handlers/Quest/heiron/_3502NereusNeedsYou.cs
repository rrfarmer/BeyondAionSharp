using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author vlog
    /// </summary>
    public class _3502NereusNeedsYou : AbstractQuestHandler
    {
        private static readonly int[] npcs = { 204656, 203752, 730192 };
        private static readonly int[] mobs = { 214894, 214895, 214896, 214897, 214904 };

        public _3502NereusNeedsYou() : base(3502)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204656).AddOnQuestStart(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            foreach (int mob in mobs)
            {
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            }
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            Npc npc = (Npc)env.GetVisibleObject();
            int targetId = npc.GetNpcId();
            int dialogActionId = env.GetDialogActionId();

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204656)
                { // Maloren
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 730192: // Balaur Operation Orders
                        if (dialogActionId == DialogAction.USE_OBJECT && var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, 0, 1); // 1
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204656)
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
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
            Npc npc = (Npc)env.GetVisibleObject();
            int targetId = npc.GetNpcId();
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                int var1 = qs.GetQuestVarById(1);
                int var2 = qs.GetQuestVarById(2);
                int var3 = qs.GetQuestVarById(3);

                switch (targetId)
                {
                    case 214894: // Telepathy Controller
                        if (var == 1)
                            return DefaultOnKillEvent(env, 214894, 1, 2, 0); // 2
                        break;
                    case 214895: // Main Power Generator
                        if (var == 2 && var1 != 1)
                        {
                            DefaultOnKillEvent(env, 214895, 0, 1, 1); // 1: 1
                            return true;
                        }
                        break;
                    case 214896: // Auxiliary Power Generator
                        if (var == 2 && var2 != 1)
                        {
                            DefaultOnKillEvent(env, 214896, 0, 1, 2); // 2: 1
                            return true;
                        }
                        break;
                    case 214897: // Emergency Generator
                        if (var == 2 && var3 != 1)
                        {
                            DefaultOnKillEvent(env, 214897, 0, 1, 3); // 3: 1
                            return true;
                        }
                        break;
                    case 214904: // Brigade General Anuhart
                        if (var == 2 && var1 == 1 && var2 == 1 && var3 == 1)
                        {
                            return DefaultOnKillEvent(env, 214904, 2, true); // reward
                        }
                        break;
                }
            }
            return false;
        }
    }
}
