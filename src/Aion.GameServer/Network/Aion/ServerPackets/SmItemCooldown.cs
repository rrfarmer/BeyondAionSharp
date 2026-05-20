using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmItemCooldown : GameServerPacket
{
	public const int PacketOpCode = 103;

	private readonly IReadOnlyDictionary<int, PlayerItemCooldown> _cooldowns;
	private readonly Func<DateTimeOffset> _clock;

	public SmItemCooldown(
		IReadOnlyDictionary<int, PlayerItemCooldown> cooldowns,
		Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ITEM_COOLDOWN(Map<Integer, ItemCooldown>).
		_cooldowns = cooldowns;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ITEM_COOLDOWN.writeImpl.
		buffer.WriteH(_cooldowns.Count);
		var nowMillis = _clock().ToUnixTimeMilliseconds();
		foreach (var (delayId, cooldown) in _cooldowns)
		{
			buffer.WriteH(delayId);
			var left = (int)((cooldown.GetReuseTime() - nowMillis) / 1000);
			buffer.WriteD(left > 0 ? left : 0);
			buffer.WriteD(cooldown.GetUseDelay());
		}
	}
}
