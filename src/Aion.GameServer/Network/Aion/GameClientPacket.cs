using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion;

public abstract class GameClientPacket
{
	protected GameClientPacket(int opCode, IReadOnlySet<GameConnectionState> validStates)
	{
		OpCode = opCode;
		ValidStates = validStates;
	}

	public int OpCode { get; }

	public IReadOnlySet<GameConnectionState> ValidStates { get; }

	public bool IsValid(GameConnectionState state) => ValidStates.Contains(state);

	public void ReadFrom(PacketBuffer buffer)
	{
		// Java parity: network/aion/AionClientPacket.readImpl dispatch.
		ReadPayload(buffer);
	}

	protected abstract void ReadPayload(PacketBuffer buffer);
}
