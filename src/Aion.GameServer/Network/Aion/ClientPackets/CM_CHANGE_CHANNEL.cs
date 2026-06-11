using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHANGE_CHANNEL (ATracer). Requests a world-channel change (FastTrack twin-count offset). WorldMapInstance/TeleportService red-tolerated.</summary>
public class CM_CHANGE_CHANNEL : AionClientPacket
{
    private int channel;

    public CM_CHANGE_CHANNEL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        channel = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        WorldMapInstance instance = activePlayer.GetPosition().GetWorldMapInstance();
        if (WorldConfig.WORLD_EMULATE_FASTTRACK && !instance.IsBeginnerInstance())
        {
            WorldMapTemplate template = instance.GetTemplate();
            // channel index starts from there
            channel += template.GetTwinCount() - 1;
        }
        TeleportService.ChangeChannel(activePlayer, channel);
    }
}
