using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRecipeCooldown : GameServerPacket
{
	public const int PacketOpCode = 165;

	private readonly int _mode;
	private readonly IReadOnlyDictionary<int, long> _cooldowns;
	private readonly Func<DateTimeOffset> _clock;

	public SmRecipeCooldown(IReadOnlyDictionary<int, long> cooldownExpirationMillisByDelayId, int mode, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RECIPE_COOLDOWN(Player, int).
		_cooldowns = cooldownExpirationMillisByDelayId;
		_mode = mode;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_RECIPE_COOLDOWN.writeImpl.
		buffer.WriteC(_mode);
		buffer.WriteH(_cooldowns.Count);
		var nowMillis = _clock().ToUnixTimeMilliseconds();
		foreach (var (delayId, expirationMillis) in _cooldowns)
		{
			buffer.WriteD(delayId);
			buffer.WriteD(GetRemainingSeconds(expirationMillis, nowMillis));
		}
	}

	private static int GetRemainingSeconds(long expirationTimeMillis, long nowMillis)
	{
		return expirationTimeMillis == 0
			? 0
			: (int)Math.Max(0, (expirationTimeMillis - nowMillis) / 1000);
	}
}
