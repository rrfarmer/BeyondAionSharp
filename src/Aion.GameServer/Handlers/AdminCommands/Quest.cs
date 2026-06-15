using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Quest (MrPoke, Neon, Pad).</summary>
public class Quest : AdminCommand
{
    public Quest()
        : base("quest", "Handles quest states of your target.")
    {
        SetSyntaxInfo(
            "[player] <quest> <reset|start|delete> - Resets/starts/deletes the specified quest.",
            "[player] <quest> <status> - Shows the quest status of the specified quest.",
            "[player] <quest> <set> <status> <var> [varNum] - Sets the specified quest state (default: apply var to all varNums, optional: set var to varNum [0-5]).",
            "[player] <quest> <setflags> <flags> - Sets the specified quest flags.",
            "[player] <quest> <dialog> <dialog_page_id> - Sends the dialog page with the given page ID.",
            "Note: If no player parameter is given, your current target will be taken (defaults to your character, if no player is targeted)."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        byte index = 0;
        Player target;
        int questId = ChatUtil.GetQuestId(paramsArr[index]);
        if (questId == 0)
        {
            target = World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[index]));

            if (target == null || !target.IsOnline())
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_MSG_ASK_PCINFO_LOGOFF());
                return;
            }

            if (++index >= paramsArr.Length)
            {
                SendInfo(admin);
                return;
            }

            questId = ChatUtil.GetQuestId(paramsArr[index]);
        }
        else
        {
            target = admin.GetTarget() is Player p ? p : admin;
        }

        if (questId == 0 || DataManager.QUEST_DATA.GetQuestById(questId) == null)
        {
            SendInfo(admin, "Invalid quest.");
            return;
        }

        if (++index >= paramsArr.Length)
        {
            SendInfo(admin);
            return;
        }

        // quest and target are both valid at this point
        if (paramsArr[index].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            ResetQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            StartQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            DeleteQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ShowQuestStatus(admin, target, questId);
        }
        else if (paramsArr[index].Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            QuestStatus status;
            int var;
            int varNum = -1;

            try
            {
                status = Enum.Parse<QuestStatus>(paramsArr[++index].ToUpper());
            }
            catch (ArgumentException)
            {
                SendInfo(admin, "<status> is one of " + "[" + string.Join(", ", Enum.GetNames(typeof(QuestStatus))) + "]");
                return;
            }
            catch (IndexOutOfRangeException)
            {
                SendInfo(admin);
                return;
            }

            try
            {
                var = int.Parse(paramsArr[++index]);
            }
            catch (FormatException)
            {
                SendInfo(admin, "<var> must be an int value.");
                return;
            }
            catch (IndexOutOfRangeException)
            {
                SendInfo(admin);
                return;
            }

            if (++index < paramsArr.Length)
            { // optional
                try
                {
                    varNum = int.Parse(paramsArr[index]);
                    if (varNum < 0 || varNum > 5)
                        throw new ArgumentException();
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException)
                { // also catches NumberFormatException
                    SendInfo(admin, "[varNum] must be an int value from 0 to 5.");
                    return;
                }
            }

            SetQuestStatus(admin, target, questId, status, var, varNum);
        }
        else if (paramsArr[index].Equals("setflags", StringComparison.OrdinalIgnoreCase))
        {
            int flags;

            try
            {
                flags = int.Parse(paramsArr[++index]);
            }
            catch (Exception e) when (e is IndexOutOfRangeException || e is FormatException)
            {
                SendInfo(admin, "<flags> must be an int value.");
                return;
            }

            SetQuestFlags(admin, target, questId, flags);
        }
        else if (paramsArr[index].Equals("dialog", StringComparison.OrdinalIgnoreCase))
        {
            int dialogPageId;

            try
            {
                dialogPageId = int.Parse(paramsArr[++index]);
            }
            catch (Exception e) when (e is IndexOutOfRangeException || e is FormatException)
            {
                SendInfo(admin, "<dialog_page_id> must be an int value.");
                return;
            }

            SendQuestDialog(admin, questId, dialogPageId);
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void ResetQuest(Player admin, Player target, int questId)
    {
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            SendInfo(admin, "Only currently active quests can be reset.");
            return;
        }
        if (qs.GetQuestVars().GetQuestVars() == 0 && qs.GetRewardGroup() == null)
        {
            SendInfo(admin, "Player " + target.GetName() + "'s quest is already at the beginning.");
            return;
        }
        qs.SetStatus(QuestStatus.START);
        qs.SetQuestVar(0);
        qs.SetRewardGroup(null);
        PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        SendInfo(admin, "Reset " + ChatUtil.Quest(questId) + " for player " + target.GetName() + ".");
    }

    private void StartQuest(Player admin, Player target, int questId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (template.GetNpcFactionId() > 0)
        {
            StartNpcFactionQuest(admin, target, questId, template.GetNpcFactionId());
            return;
        }
        else if (QuestService.StartQuest(new QuestEnv(null, target, questId)))
        {
            SendInfo(admin, "Started " + ChatUtil.Quest(questId) + " for player " + target.GetName() + ".");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs != null && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.REWARD))
        {
            SendInfo(admin, "Quest is already started.");
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.COMPLETE && !qs.CanRepeat())
        {
            SendInfo(admin, "Quest is already completed.");
        }
        else
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<XMLStartCondition> preconditions = template.GetXMLStartConditions();
            if (preconditions != null)
            {
                foreach (XMLStartCondition condition in preconditions)
                {
                    List<FinishedQuestCond> finisheds = condition.GetFinishedPreconditions();
                    if (finisheds != null)
                    {
                        foreach (FinishedQuestCond fcondition in finisheds)
                        {
                            QuestState qs1 = target.GetQuestStateList().GetQuestState(fcondition.GetQuestId());
                            if (qs1 == null || qs1.GetStatus() != QuestStatus.COMPLETE)
                            {
                                sb.Append("\n\t" + ChatUtil.Quest(fcondition.GetQuestId()));
                            }
                        }
                    }
                }
            }
            SendInfo(admin,
                "Quest not started. " + (sb.Length > 0 ? "These quest(s) must be completed first:" + sb.ToString() : "Some preconditions failed."));
        }
    }

    private void StartNpcFactionQuest(Player admin, Player target, int questId, int factionId)
    {
        NpcFaction faction = target.GetNpcFactions().GetActiveNpcFaction(false);
        if (faction == null || faction.GetId() != factionId)
        {
            SendInfo(admin, "Player " + target.GetName() + " is not registered to the organization for this quest.");
            return;
        }
        foreach (QuestTemplate template in DataManager.QUEST_DATA.GetQuestsByNpcFaction(faction.GetId(), target))
        {
            if (template.GetId() == questId)
            {
                // simulate daily reset
                faction.SetActive(false);
                faction.SetTime(-1);
                target.GetNpcFactions().AddNpcFaction(faction);
                faction.SetActive(true);
                // set daily quest Id and time to avoid random quest
                faction.SetState(ENpcFactionQuestState.NOTING);
                faction.SetQuestId(questId);
                faction.SetTime(faction.GetTime() + 100);
                // send the daily quest to player
                target.GetNpcFactions().SendDailyQuest();
                SendInfo(admin, "Started npc faction quest " + ChatUtil.Quest(questId) + " for player " + target.GetName() + ".");
                return;
            }
        }
        SendInfo(admin, "Quest not implemented or player level doesn't match.");
    }

    private void DeleteQuest(Player admin, Player target, int questId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().DeleteQuest(questId);
        if (qs == null)
        {
            SendInfo(admin, "Player " + target.GetName() + " does not have that quest.");
            return;
        }
        if (qs.GetStatus() == QuestStatus.COMPLETE)
            QuestEngine.QuestEngine.GetInstance().SendCompletedQuests(target); // rewrite completed quest list
        else
            PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ABANDON, qs));
        target.GetController().UpdateNearbyQuests();
        SendInfo(admin, "Deleted " + ChatUtil.Quest(questId) + " for player " + target.GetName() + ".");
    }

    private void ShowQuestStatus(Player admin, Player target, int questId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Player: " + target.GetName() + ", quest: " + ChatUtil.Quest(questId) + "\n\tQuest status: ");
        if (qs == null)
        {
            sb.Append("NULL");
        }
        else
        {
            sb.Append(qs.GetStatus().ToString());
            sb.Append("\n\tQuest vars:");
            for (int i = 0; i <= 5; i++)
                sb.Append(" " + qs.GetQuestVarById(i));
            sb.Append(", encoded [" + qs.GetQuestVars().GetQuestVars() + "]");
            sb.Append("\n\tQuest flags: " + (qs.GetFlags() & 0x3F) + " " + qs.GetStepGroup()); // needs rework when flags are implemented like vars
            sb.Append(", encoded [" + qs.GetFlags() + "]");
        }
        SendInfo(admin, sb.ToString());
    }

    private void SetQuestStatus(Player admin, Player target, int questId, QuestStatus status, int var, int varNum)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        SM_QUEST_ACTION.ActionType actionType;
        if (qs == null)
        { // player doesn't have that quest
            actionType = SM_QUEST_ACTION.ActionType.ADD;
            qs = new QuestState(questId, status);
            target.GetQuestStateList().AddQuest(questId, qs);
        }
        else
        {
            actionType = qs.GetStatus() == QuestStatus.COMPLETE ? SM_QUEST_ACTION.ActionType.ADD : SM_QUEST_ACTION.ActionType.UPDATE;
            qs.SetStatus(status);
        }
        if (status == QuestStatus.COMPLETE)
        {
            qs.SetQuestVar(0); // completed quests vars are always 0
            if (DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId()).GetRewards().Count != 0)
                qs.SetRewardGroup(0); // follow quests could require reward group > 0 to be unlocked (see quest_data.xml)
            QuestEngine.QuestEngine.GetInstance().OnQuestCompleted(target, questId);
        }
        else
        {
            if (varNum == -1)
                qs.SetQuestVar(var);
            else
                qs.SetQuestVarById(varNum, var);
        }
        if (actionType == SM_QUEST_ACTION.ActionType.ADD && status == QuestStatus.COMPLETE)
            PacketSendUtility.SendPacket(target, new SM_QUEST_COMPLETED_LIST(1, new List<QuestState> { qs }));
        else
            PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(actionType, qs));
        target.GetController().UpdateNearbyQuests();
        SendInfo(admin, "Set quest status of " + ChatUtil.Quest(questId) + " for player " + target.GetName() + ".");
    }

    private void SetQuestFlags(Player admin, Player target, int questId, int flags)
    { // needs rework when flags are implemented like vars
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            SendInfo(admin, "Flags can only be set for active quests.");
            return;
        }
        qs.SetFlags(flags);
        PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        SendInfo(admin, "Set " + target.GetName() + "'s quest flags to " + flags + ".");
    }

    private void SendQuestDialog(Player admin, int questId, int dialogPageId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        PacketSendUtility.SendPacket(admin, new SM_DIALOG_WINDOW(0, dialogPageId, questId));
        SendInfo(admin, "Sent dialog page " + dialogPageId + " of Q" + questId + ".");
    }
}
