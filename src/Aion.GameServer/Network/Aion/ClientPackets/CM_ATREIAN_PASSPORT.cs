using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ATREIAN_PASSPORT (ViAl). Reads passport-id/timestamp pairs (count -1 = read until exhausted) and claims rewards. AtreianPassportService red-tolerated.</summary>
public class CM_ATREIAN_PASSPORT : AionClientPacket
{
    private Dictionary<int, HashSet<int>> passports = new Dictionary<int, HashSet<int>>();

    public CM_ATREIAN_PASSPORT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        int count = ReadH();
        for (int i = 0; i < count || count == -1; i++)
        {
            if (GetRemainingBytes() < 8)
            {
                if (count != -1)
                    NullLoggerFactory.Instance.CreateLogger(nameof(CM_ATREIAN_PASSPORT)).LogWarning("Received invalid passport count " + count + " with only data for " + i
                        + " passports from " + GetConnection().GetActivePlayer() + "\nCurrent passport data: " + passports);
                break;
            }
            int passportId = ReadD();
            int timestamp = ReadD();
            if (!passports.TryGetValue(passportId, out HashSet<int> timestamps))
            {
                timestamps = new HashSet<int>();
                passports[passportId] = timestamps;
            }
            timestamps.Add(timestamp);
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player != null)
            AtreianPassportService.GetInstance().TakeReward(player, passports);
    }
}
