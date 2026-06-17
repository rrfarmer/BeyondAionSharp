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

/// <summary>
/// @author VladimirZ
/// </summary>
public class _2938SecretLibraryAccess : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204267, 203557 }; // Oubliette and Suthran

    public _2938SecretLibraryAccess() : base(2938)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204267).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
        {
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }
    }

    private bool AreAltgardQuestsFinished(Player player)
    {
        int id = player.GetQuestStateList().GetQuestState(2200) != null ? 2022 : 24016; // 2022 Old path, 24016 New path
        QuestState qs = player.GetQuestStateList().GetQuestState(id); // last quest in Altgard state
        return qs != null && qs.GetStatus() == QuestStatus.COMPLETE;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 204267)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT && qs.GetStatus() == QuestStatus.REWARD)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                {
                    RemoveQuestItem(env, 182207026, 1);
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    return SendQuestEndDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.COMPLETE)
            {
                TeleportService.TeleportTo(player, WorldMapType.PANDAEMONIUM.GetId(), 1403.2f, 1063.7f, 206.0f, (byte)89, TeleportAnimation.FADE_OUT_BEAM);
                return true;
            }
        }
        else if (targetId == 203557)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (AreAltgardQuestsFinished(player))
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else
                        return SendQuestDialog(env, 1097);
                }
                else if (env.GetDialogActionId() == DialogAction.SET_SUCCEED)
                {
                    if (GiveQuestItem(env, 182207026, 1))
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                    }
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        return false;
    }
}
