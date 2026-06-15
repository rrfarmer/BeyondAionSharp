using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/iceFestival/IshrunerkAI.</summary>
[AIName("ishrunerk")]
public class IshrunerkAI : GeneralNpcAI
{
    public IshrunerkAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        base.HandleDialogStart(player);
        // custom dialog handling because the quests don't show up in the quest selection (incomplete quest data in the 4.8 game client)
        foreach (int questId in new List<int> { 80715, 80716, 80717, 80718, 80720 })
        {
            if (Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(new QuestEnv(GetOwner(), player, questId, DialogAction.QUEST_SELECT)))
                break;
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SELECTED_QUEST_NOREWARD)
        {
            QuestEnv env = new QuestEnv(GetOwner(), player, questId, dialogActionId);
            env.SetExtendedRewardIndex(extendedRewardIndex);
            if (Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(env))
                PacketSendUtility.SendPacket(env.GetPlayer(), new SM_DIALOG_WINDOW(GetObjectId(), 0, questId)); // close window
            return true;
        }
        return base.OnDialogSelect(player, dialogActionId, questId, extendedRewardIndex);
    }
}
