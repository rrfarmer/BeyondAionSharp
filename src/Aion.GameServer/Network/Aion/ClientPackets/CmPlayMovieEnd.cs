using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPlayMovieEnd : GameClientPacket
{
	public CmPlayMovieEnd(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Type { get; private set; }

	public int TargetObjectId { get; private set; }

	public int QuestId { get; private set; }

	public int MovieId { get; private set; }

	public byte Unknown { get; private set; }

	public bool CanSkip { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PLAY_MOVIE_END.readImpl.
		Type = buffer.ReadC();
		TargetObjectId = buffer.ReadD();
		QuestId = buffer.ReadD();
		MovieId = buffer.ReadD();
		Unknown = buffer.ReadC();
		CanSkip = buffer.ReadC() == 0;
	}
}
