using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/Door2AI (Gigi, Neon).</summary>
[AIName("door2")]
public class Door2AI : ActionItemNpcAI
{
    public Door2AI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        int questId = player.GetRace() == Race.ELYOS ? 30211 : 30311;
        // Only one player in group has to have this quest
        foreach (Player member in player.IsInGroup() ? player.GetPlayerGroup().GetOnlineMembers() : new List<Player> { player })
        {
            if (member.GetQuestStateList().HasQuest(questId))
            {
                base.HandleDialogStart(player);
                return;
            }
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
    }

    protected override void HandleUseItemFinish(Player player)
    {
        AIActions.DeleteOwner(this);
    }
}
