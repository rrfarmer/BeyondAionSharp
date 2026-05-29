using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmShieldEffect : GameServerPacket
{
	public const int PacketOpCode = 218;
	private readonly IReadOnlyList<ShieldEffectLocationSnapshot> _locations;

	public SmShieldEffect(IEnumerable<ShieldEffectLocationSnapshot> locations) : base(PacketOpCode)
	{
		ArgumentNullException.ThrowIfNull(locations);
		_locations = locations.ToList();
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SHIELD_EFFECT.writeImpl writes
		// location count followed by each SiegeLocation id and under-shield flag.
		buffer.WriteH(_locations.Count);
		foreach (var location in _locations)
		{
			buffer.WriteD(location.LocationId);
			buffer.WriteC(location.IsUnderShield ? 1 : 0);
		}
	}
}

public sealed record ShieldEffectLocationSnapshot(int LocationId, bool IsUnderShield);
