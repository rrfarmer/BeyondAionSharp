using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_UI_SETTINGS (ATracer). Persists UI settings (0) / shortcuts (1) / house buddies (2). PlayerSettings red-tolerated.</summary>
public class CM_UI_SETTINGS : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_UI_SETTINGS));
    private byte settingsType;
    private byte[] data;
    private int size;

    public CM_UI_SETTINGS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        settingsType = ReadC();
        ReadH();
        size = ReadUH();
        data = ReadB(GetRemainingBytes());
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (settingsType)
        {
            case 0:
                player.GetPlayerSettings().SetUiSettings(data);
                break;
            case 1:
                player.GetPlayerSettings().SetShortcuts(data);
                break;
            case 2:
                player.GetPlayerSettings().SetHouseBuddies(data);
                break;
            default:
                log.LogWarning(player + " sent unknown type of player settings: " + settingsType);
                break;
        }
    }
}
