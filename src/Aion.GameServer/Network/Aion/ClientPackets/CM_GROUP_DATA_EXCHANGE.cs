using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GROUP_DATA_EXCHANGE (xTz). Relays opaque group/alliance/league UI exchange data (action 1 = broadcast-and-receive). SM_GROUP_DATA_EXCHANGE/NetworkUtils red-tolerated.</summary>
public class CM_GROUP_DATA_EXCHANGE : AionClientPacket
{
    /// <summary>
    /// Maximum size of the exchange data. The size is determined by subtracting the maximum usable packet body size in bytes by the overhead bytes
    /// required to send it via SM_GROUP_DATA_EXCHANGE
    /// </summary>
    private static readonly int MAX_EXCHANGE_DATA_SIZE = AionServerPacket.MAX_USABLE_PACKET_BODY_SIZE - 6;

    private int groupType;
    private int action;
    private int unk2;
    private byte[] data;

    public CM_GROUP_DATA_EXCHANGE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadUC();
        if (action != 1)
        {
            groupType = ReadUC();
            unk2 = ReadUC();
        }
        int dataSize = ReadD();
        data = ReadB(dataSize);
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null || data.Length == 0)
            return;

        if (data.Length > MAX_EXCHANGE_DATA_SIZE)
        {
            NullLoggerFactory.Instance.CreateLogger(nameof(CM_GROUP_DATA_EXCHANGE)).LogError(
                "Player {Player} exceeded maximum exchange data size (action: {Action}, groupType: {GroupType}, unk2: {Unk2}, bytes send: {Bytes}): \n{Hex}", player, action, groupType, unk2,
                data.Length, NetworkUtils.ToHex(data));
            return;
        }

        if (action == 1)
        {
            PacketSendUtility.BroadcastPacketAndReceive(player, new SM_GROUP_DATA_EXCHANGE(data));
            return;
        }
        List<Player> players = null;
        switch (groupType)
        {
            case 0:
                if (player.IsInGroup())
                    players = player.GetPlayerGroup().GetOnlineMembers();
                break;
            case 1:
                if (player.IsInAlliance())
                    players = player.GetPlayerAllianceGroup().GetOnlineMembers();
                break;
            case 2:
                if (player.IsInLeague())
                    players = player.GetPlayerAllianceGroup().GetOnlineMembers();
                break;
        }

        if (players == null || players.Count == 0)
            return;
        SM_GROUP_DATA_EXCHANGE packet = new SM_GROUP_DATA_EXCHANGE(data, action, unk2);
        foreach (Player member in players)
        {
            if (!member.Equals(player))
                PacketSendUtility.SendPacket(member, packet);
        }
    }
}
