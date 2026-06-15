using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author MrPoke + Dune11, vlog
    /// </summary>
    public class _1007ACeremonyinSanctum : AbstractQuestHandler
    {
        public _1007ACeremonyinSanctum() : base(1007)
        {
        }

        public override void Register()
        {
            if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)
                return;
            int[] npcs = { 790001, 203725, 203752, 203758, 203759, 203760, 203761, 801212, 801213 };
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnQuestCompleted(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();
            if (qs == null)
            {
                return false;
            }
            int var = qs.GetQuestVars().GetQuestVars();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 790001: // Pernos
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1013);
                                }
                                return false;
                            case DialogAction.SETPRO1:
                                if (var <= 1)
                                {
                                    qs.SetQuestVar(1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                    TeleportService.TeleportTo(player, 110010000, 1313f, 1512f, 568f, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
                                    return true;
                                }
                                break;
                        }
                        break;
                    case 203725: // Leah
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                return false;
                            case DialogAction.SELECT2_1:
                                return PlayQuestMovie(env, 92);
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2); // 2
                        }
                        break;
                    case 203752: // Jucleas
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                return false;
                            case DialogAction.SELECT3_1:
                                return PlayQuestMovie(env, 91);
                            case DialogAction.SETPRO3:
                                if (var == 2)
                                {
                                    switch (player.GetPlayerClass().GetStartingClass())
                                    {
                                        case PlayerClass.WARRIOR:
                                            qs.SetQuestVar(10);
                                            qs.SetRewardGroup(0);
                                            break;
                                        case PlayerClass.SCOUT:
                                            qs.SetQuestVar(20);
                                            qs.SetRewardGroup(1);
                                            break;
                                        case PlayerClass.MAGE:
                                            qs.SetQuestVar(30);
                                            qs.SetRewardGroup(2);
                                            break;
                                        case PlayerClass.PRIEST:
                                            qs.SetQuestVar(40);
                                            qs.SetRewardGroup(3);
                                            break;
                                        case PlayerClass.ENGINEER:
                                            qs.SetQuestVar(50);
                                            qs.SetRewardGroup(4);
                                            break;
                                        case PlayerClass.ARTIST:
                                            qs.SetQuestVar(60);
                                            qs.SetRewardGroup(5);
                                            break;
                                    }
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestSelectionDialog(env);
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203758 && var == 10)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2034);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 203759 && var == 20)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2375);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 203760 && var == 30)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2716);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 203761 && var == 40)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3057);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 801212 && var == 50)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3398);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 801213 && var == 60)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3739);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 1006);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 1006);
        }
    }
}
