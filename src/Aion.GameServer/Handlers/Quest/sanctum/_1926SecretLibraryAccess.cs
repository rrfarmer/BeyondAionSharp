using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

public class _1926SecretLibraryAccess : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 203894, 203701 }; // Latri and Lavirintos

    public _1926SecretLibraryAccess() : base(1926)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203894).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
        {
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }
    }

    private bool AreVerteronQuestsFinished(Player player)
    {
        int id = player.GetQuestStateList().GetQuestState(1130) != null ? 1020 : 14016; // 1020 Old path, 14016 New path
        QuestState qs = player.GetQuestStateList().GetQuestState(id); // last quest in Verteron state
        return qs != null && qs.GetStatus() == QuestStatus.COMPLETE;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 203894)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD && qs.GetQuestVarById(0) == 0 || qs.GetStatus() == QuestStatus.COMPLETE)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT && qs.GetStatus() == QuestStatus.REWARD)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                {
                    RemoveQuestItem(env, 182206022, 1);
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    return SendQuestEndDialog(env);
                }
                TeleportService.TeleportTo(player, WorldMapType.SANCTUM.GetId(), 2032.9f, 1473.1f, 592.2f, (byte)119, TeleportAnimation.FADE_OUT_BEAM);
                return true;
            }
        }
        else if (targetId == 203701)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (AreVerteronQuestsFinished(player))
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else
                        return SendQuestDialog(env, 1097);
                }
                else if (env.GetDialogActionId() == DialogAction.SET_SUCCEED)
                {
                    if (GiveQuestItem(env, 182206022, 1))
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                    }
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        return false;
    }
}
