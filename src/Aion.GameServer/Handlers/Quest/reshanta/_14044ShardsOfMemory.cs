using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14044ShardsOfMemory : AbstractQuestHandler
    {
        public _14044ShardsOfMemory() : base(14044)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(279029).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(278501).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(790001).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(700355).AddOnTalkEvent(questId);
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
            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 278501:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SETPRO1:
                                if (var == 0)
                                {
                                    ChangeQuestStep(env, 0, 1);
                                    TeleportService.TeleportTo(player, 210010000, 244.09f, 1638.28f, 100.38f, (byte)52, TeleportAnimation.FADE_OUT_BEAM);
                                    return true;
                                }
                                break;
                        }
                        break;
                    case 279029:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 1693);
                                return false;
                            case DialogAction.SETPRO3:
                                if (var == 2)
                                {
                                    qs.SetQuestVarById(0, var + 1);
                                    UpdateQuestStatus(env);
                                    PlayQuestMovie(env, 271);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                    return true;
                                }
                                break;
                        }
                        break;
                    case 700355:
                        if (var == 3)
                        {
                            if (CheckItemExistence(env, 188020000, 1, true))
                                return UseQuestObject(env, 3, 3, true, false);
                            PacketSendUtility.SendMonologue(player, 1111203); // I need an Artifact Activation Stone!
                        }
                        break;
                    case 790001:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SETPRO2:
                                if (var == 1)
                                {
                                    ChangeQuestStep(env, 1, 2);
                                    TeleportService.TeleportTo(player, 400010000, 2929.65f, 964.836f, 1538.17f, (byte)43, TeleportAnimation.FADE_OUT_BEAM);
                                    return true;
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 279029)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            }
            return false;
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14040);
        }
    }
}
