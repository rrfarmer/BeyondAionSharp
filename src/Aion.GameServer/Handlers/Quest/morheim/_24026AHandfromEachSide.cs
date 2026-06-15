using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// Talk with Aegir (204301). Meet Taisan (204403). Pass through Morheim Abyss Gate and talk with Kargate (204423). Protect Kargate from the Balaur:
    /// &lt;spawnpos: 254.21326, 256.9302, 226.6418, 93&gt;. Draconute Scout (280818), Crusader (211624), Chandala Scaleguard (213578), Chandala Fangblade
    /// (213579). Speak to Kargate. Report back to Aegir.
    ///
    /// @author Ritsu, Pad
    /// </summary>
    public class _24026AHandfromEachSide : AbstractQuestHandler
    {
        private static readonly int[] mobIds = { 213575, 280818 };
        private int balaurKilled = 0;

        public _24026AHandfromEachSide() : base(24026)
        {
        }

        public override void Register()
        {
            int[] npcIds = { 204301, 204403, 204432 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnQuestTimerEnd(questId);
            qe.RegisterOnLogOut(questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npcId in npcIds)
                qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
            foreach (int mob in mobIds)
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            int[] quests = { 24025, 24024, 24023, 24022, 24021, 24020 };
            DefaultOnQuestCompletedEvent(env, quests);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            int[] quests = { 24025, 24024, 24023, 24022, 24021, 24020 };
            DefaultOnLevelChangedEvent(player, quests);
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
                if (targetId == 204301) // Aegir
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 2375);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 204301: // Aegir
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SETPRO1:
                                DefaultCloseDialog(env, 0, 1); // 1
                                TeleportService.TeleportTo(player, 220020000, 2794.55f, 477.6f, 265.65f, (byte)40, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                        }
                        break;
                    case 204403: // Taisan
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SETPRO2:
                                DefaultCloseDialog(env, 1, 2); // 2
                                TeleportService.TeleportTo(player, 220020000, 3030.5f, 875.5f, 363.0f, (byte)12, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                        }
                        break;
                    case 204432: // Kargate
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 1693);
                                else if (var == 4)
                                    return SendQuestDialog(env, 2034);
                                return false;
                            case DialogAction.SETPRO3:
                                balaurKilled = 0;
                                Spawn(213575, player, 248.78f, 259.28f, 227.74f, (byte)94); // Crusader
                                Spawn(213575, player, 259.10f, 261.79f, 227.77f, (byte)94); // 280818 Draconute Scout
                                QuestService.QuestTimerStart(env, 240);
                                return DefaultCloseDialog(env, 2, 3); // 3
                            case DialogAction.SETPRO4:
                                if (var == 4)
                                {
                                    qs.SetRewardGroup(0); // set default reward group to suppress warnings (both possible reward groups are identical)
                                    DefaultCloseDialog(env, 4, 4, true, false); // reward
                                    TeleportService.TeleportTo(player, 220020000, 3030.8676f, 875.6538f, 363.2065f, (byte)73, TeleportAnimation.FADE_OUT_BEAM);
                                    return true;
                                }
                                break;
                        }
                        break;
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            if (qs.GetQuestVarById(0) == 3)
            {
                int targetId = env.GetTargetId();
                if (targetId == 213575 || targetId == 280818)
                {
                    balaurKilled++;
                    if (balaurKilled == 2)
                    {
                        Spawn(213575, player, 248.78f, 259.28f, 227.74f, (byte)94); // Crusader
                        Spawn(213575, player, 259.10f, 261.79f, 227.77f, (byte)94); // 280818 Draconute Scout
                    }
                    else if (balaurKilled == 4)
                    {
                        QuestService.QuestTimerEnd(env);
                        if (KargateIsAlive(env))
                        {
                            ChangeQuestStep(env, 3, 4);
                            PlayQuestMovie(env, 158);
                        }
                        else
                        {
                            ChangeQuestStep(env, 3, 2);
                            Spawn(204432, player, 272.83f, 176.81f, 204.35f, (byte)0);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override bool OnQuestTimerEndEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            if (qs.GetQuestVarById(0) == 3)
            {
                DeleteBalaur(env);
                if (KargateIsAlive(env))
                {
                    ChangeQuestStep(env, 3, 4);
                    PlayQuestMovie(env, 158);
                }
                else
                {
                    ChangeQuestStep(env, 3, 2);
                    Spawn(204432, player, 272.83f, 176.81f, 204.35f, (byte)0);
                }
                return true;
            }
            return false;
        }

        public override bool OnLogOutEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            if (qs.GetQuestVarById(0) == 3)
            {
                DeleteBalaur(env);
                QuestService.QuestTimerEnd(env);
                ChangeQuestStep(env, 3, 2);
                if (!KargateIsAlive(env))
                    Spawn(204432, player, 272.83f, 176.81f, 204.35f, (byte)0);
                return true;
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            if (qs.GetQuestVarById(0) == 3 && player.GetWorldId() != 320040000)
            {
                QuestService.QuestTimerEnd(env);
                ChangeQuestStep(env, 3, 2);
                return true;
            }
            return false;
        }

        private bool KargateIsAlive(QuestEnv env)
        {
            Npc kargate = env.GetPlayer().GetWorldMapInstance().GetNpc(204432);
            return (kargate != null && !kargate.IsDead());
        }

        private void DeleteBalaur(QuestEnv env)
        {
            foreach (Npc npc in env.GetPlayer().GetWorldMapInstance().GetNpcs(213575, 280818))
            {
                npc.GetController().Delete();
            }
        }
    }
}
