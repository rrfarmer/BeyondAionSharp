using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.DataHolders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DIALOG_SELECT (KKnD, orz, avol, Pad). Handles a dialog/quest action selection against self or an NPC/creature target. QuestEngine/QuestService/DialogService/DataManager red-tolerated.</summary>
public class CM_DIALOG_SELECT : AionClientPacket
{
    /// <summary>Target object id that client wants to TALK WITH or 0 if wants to unselect</summary>
    private int targetObjectId;
    private int dialogActionId;
    private int extendedRewardIndex;
    private int lastPage;
    private int questId;
    private int unk;

    public CM_DIALOG_SELECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
        dialogActionId = ReadUH();
        extendedRewardIndex = ReadUH();
        lastPage = ReadUH();
        questId = ReadD();
        unk = ReadUH(); // unk 4.7
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        if (player.IsTrading())
            return;

        string dialogActionName = DialogAction.NameOf(dialogActionId);
        if (player.HasAccess(AdminConfig.DIALOG_INFO))
        {
            PacketSendUtility.SendMessage(player, "Quest ID: " + questId + ", Dialog Action: " + dialogActionName + " (ID: " + dialogActionId + ")");
        }
        if (dialogActionName == null)
        {
            NullLoggerFactory.Instance.CreateLogger(nameof(CM_DIALOG_SELECT))
                .LogWarning("Received unknown dialog action id " + dialogActionId + " (quest " + questId + ") from " + player);
            return;
        }

        if (targetObjectId == 0 || targetObjectId == player.GetObjectId())
        {
            QuestTemplate questTemplate = DataManager.QUEST_DATA.GetQuestById(questId);
            if (questTemplate == null)
                return;

            QuestEnv env = new QuestEnv(null, player, questId, dialogActionId);
            if (questTemplate.IsCanReport())
            {
                switch (dialogActionId)
                {
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD1:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD2:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD3:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD4:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD5:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD6:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD7:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD8:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD9:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD10:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD11:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD12:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD13:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD14:
                    case DialogAction.SELECTED_QUEST_AUTO_REWARD15:
                        QuestService.FinishQuest(env);
                        return;
                }
            }
            if (QuestEngine.GetInstance().OnDialog(env))
                return;
            if (CustomConfig.ENABLE_SIMPLE_2NDCLASS && (questId == 1006 || questId == 2008))
                ClassChangeService.ChangeClassToSelection(player, dialogActionId);
            return;
        }

        if (player.GetKnownList().GetObject(targetObjectId) is Creature target)
        {
            if (target is Npc npc)
            {
                bool isFunctionDialog = DataManager.NPC_DATA.IsFunctionDialog(dialogActionId);
                if (isFunctionDialog && !npc.GetObjectTemplate().SupportsAction(dialogActionId))
                {
                    AuditLogger.Log(player, "tried to use unsupported dialog action " + dialogActionName + " on " + npc);
                    return;
                }
                if ((isFunctionDialog || dialogActionId < DialogAction.SELECT1) && !DialogService.IsInteractionAllowed(player, npc))
                {
                    AuditLogger.Log(player, "tried to illegally use dialog action " + dialogActionName + " on " + npc);
                    return;
                }
            }
            target.GetController().OnDialogSelect(dialogActionId, lastPage, player, questId, extendedRewardIndex);
        }
    }
}
