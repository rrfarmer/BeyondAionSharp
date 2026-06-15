using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/VaultKeeper (@author Yeats).</summary>
[AIName("IDSweep_Treasure_Room")]
public class VaultKeeper : GeneralNpcAI
{
    private readonly object _lock = new object();

    public VaultKeeper(Npc owner)
        : base(owner)
    {
    }

    private int room = 0;
    private readonly Dictionary<int, int> playerAndRoom = new Dictionary<int, int>();

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        CheckEntryConditions(player, dialogActionId);
        return true;
    }

    private void CheckEntryConditions(Player player, int dialogActionId)
    {
        lock (_lock)
        {
            if (dialogActionId == DialogAction.SETPRO1)
            {
                int roomNo = room;

                if (playerAndRoom.ContainsKey(player.GetObjectId()))
                {
                    roomNo = playerAndRoom[player.GetObjectId()];
                }

                if (roomNo == 0)
                {
                    playerAndRoom[player.GetObjectId()] = 0;
                    room++;
                    TeleportService.TeleportTo(player, 301400000, player.GetInstanceId(), 171.741f, 230.466f, 395f, (byte)88, TeleportAnimation.FADE_OUT_BEAM);
                }
                else if (roomNo == 1)
                {
                    playerAndRoom[player.GetObjectId()] = 1;
                    room++;
                    TeleportService.TeleportTo(player, 301400000, player.GetInstanceId(), 171.741f, 384.591f, 395f, (byte)88, TeleportAnimation.FADE_OUT_BEAM);
                }
                else if (roomNo == 2)
                {
                    playerAndRoom[player.GetObjectId()] = 2;
                    room++;
                    TeleportService.TeleportTo(player, 301400000, player.GetInstanceId(), 171.741f, 541.754f, 395f, (byte)88, TeleportAnimation.FADE_OUT_BEAM);
                }
                else
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                }
            }
        }
    }
}
