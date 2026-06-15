using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, Pad, Neon
/// </summary>
public class _28604RecoveringRotan : AbstractQuestHandler
{
    public _28604RecoveringRotan() : base(28604)
    {
    }

    public override void Register()
    {
        qe.RegisterOnEnterZone(ZoneName.Get("GRAND_CAVERN_300230000"), questId);
        qe.RegisterQuestNpc(700961).AddOnTalkEvent(questId); // Grave Robber's Corpse
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (env.GetTargetId() != 700961)
            return false;
        if (player.GetRace() != Race.ASMODIANS) // both factions use the same npc for rotan quest (see quest 18604)
            return false;

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        { // in case quest wasn't started by zone enter or player aborted
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
            {
                return SendQuestDialog(env, DialogPage.ASK_QUEST_ACCEPT_WINDOW.Id());
            }
            else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                if (QuestService.StartQuest(env))
                    return SendQuestDialog(env, 10002);
            }
            else
            {
                return CloseDialogWindow(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
            {
                return SendQuestDialog(env, 10002);
            }
            else
            {
                ChangeQuestStep(env, 0, 0, true);
                return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() == QuestStatus.COMPLETE)
        { // handling when quest is already done
            env.SetQuestId(0);
            if (CheckItemExistence(env, 164000141, 1, false)) // player already has rotan summon device
                return SendQuestDialog(env, DialogPage.NO_RIGHT.Id());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_SKILL_01());
            GiveQuestItem(env, 164000141, 1);
            return SendQuestDialog(env, 1012);
        }

        return false;
    }

    public override bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
    {
        // allow to use body even when quest is completed
        return env.GetTargetId() == 700961 && questEventType == QuestActionType.ACTION_ITEM_USE;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get("GRAND_CAVERN_300230000"))
            return false;

        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            env.SetQuestId(questId);
            return QuestService.StartQuest(env);
        }

        return false;
    }
}
