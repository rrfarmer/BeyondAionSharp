using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Status = global::Aion.GameServer.Model.GameObjects.Players.FriendList.Status;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FRIEND_STATUS (Ben). Received when a user changes their buddylist status. FriendList.Status/SM_FRIEND_STATUS red-tolerated.</summary>
public class CM_FRIEND_STATUS : AionClientPacket
{
    private readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_FRIEND_STATUS));
    // The users new status
    private byte status;

    public CM_FRIEND_STATUS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        status = ReadC();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        Status? statusEnum = FriendListStatusExtensions.GetByValue(status);
        if (statusEnum == null)
        {
            log.LogWarning("received unknown status id " + status);
            statusEnum = Status.ONLINE;
        }
        activePlayer.GetFriendList().SetStatus(statusEnum.Value, activePlayer.GetCommonData());
        SendPacket(new SM_FRIEND_STATUS(status));
    }
}
