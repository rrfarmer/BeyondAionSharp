using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAtreianPassport : GameClientPacket
{
	private readonly Dictionary<int, HashSet<int>> _passports = new();

	public CmAtreianPassport(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Count { get; private set; }

	public IReadOnlyDictionary<int, HashSet<int>> Passports => _passports;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ATREIAN_PASSPORT.readImpl.
		Count = buffer.ReadSignedH();
		for (var i = 0; i < Count || Count == -1; i++)
		{
			if (buffer.Remaining < 8)
				break;

			var passportId = buffer.ReadD();
			var timestamp = buffer.ReadD();
			if (!_passports.TryGetValue(passportId, out var timestamps))
			{
				timestamps = new HashSet<int>();
				_passports[passportId] = timestamps;
			}

			timestamps.Add(timestamp);
		}
	}
}
