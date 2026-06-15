using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Report-To-Quest Start: Perento (204500) Take the paper voucher (182213000) to Koruchinerk (798321) Go to New Heiron Gate and meet Herthia (205228)
/// Bring the Fake Stigma (182213001) to Perento
///
/// @author vlog, Gigi
/// </summary>
public class _18600ScoringSomeBadStigma : AbstractQuestHandler
{
    private static readonly int _questId = 18600;
    private static readonly int[] _npcs = { 204500, 205228, 804601 };

    public _18600ScoringSomeBadStigma() : base(18600)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204500).AddOnQuestStart(_questId);
        foreach (int npc_id in _npcs)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(_questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(_questId);

        if (targetId == 204500) // Perento
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    return SendQuestStartDialog(env, 182213000, 1);
                else
                    return SendQuestStartDialog(env, 182213000, 1);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    RemoveQuestItem(env, 182213001, 1);
                    return SendQuestEndDialog(env);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        if (targetId == 804601) // Koruchinerk
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1, 0, 0, 182213000, 1); // 1
            }
        }
        if (targetId == 205228) // Herthia
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVar(3);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    GiveQuestItem(env, 182213001, 1);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
        }
        return false;
    }
}
