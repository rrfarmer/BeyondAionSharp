using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.AntiHack;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GAMEGUARD. Reads the GameGuard binary blob and hands its size to the anti-hack check. AntiHackService red-tolerated.</summary>
public class CM_GAMEGUARD : AionClientPacket
{
    private int size;

    public CM_GAMEGUARD(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        size = ReadD();
        ReadB(size);
    }

    protected override void RunImpl()
    {
        AntiHackService.CheckAionBin(size, GetConnection());
    }
}
