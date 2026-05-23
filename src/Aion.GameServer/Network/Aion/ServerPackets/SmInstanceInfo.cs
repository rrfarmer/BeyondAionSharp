using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInstanceInfo : GameServerPacket
{
	public const int PacketOpCode = 141;

	private readonly byte _updateType;
	private readonly Player _player;
	private readonly IReadOnlyList<InstanceCooltimeSummary> _instanceCooltimes;
	private readonly Func<DateTimeOffset> _clock;
	private readonly int _singleInstanceCooldownId;

	public SmInstanceInfo(
		byte updateType,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INSTANCE_INFO(byte, Player, Integer...).
		_updateType = updateType;
		_player = player;
		_instanceCooltimes = instanceCooltimes.Templates;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_singleInstanceCooldownId = 0;
	}

	public SmInstanceInfo(
		byte updateType,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		int worldId,
		Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INSTANCE_INFO(byte, Player, Integer...) single-instance update path.
		_updateType = updateType;
		_player = player;
		_instanceCooltimes = instanceCooltimes.Templates
			.Where(cooltime => cooltime.WorldId == worldId)
			.ToArray();
		_clock = clock ?? (() => DateTimeOffset.Now);
		_singleInstanceCooldownId = _instanceCooltimes.Count == 1 ? _instanceCooltimes[0].Id : 0;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_INSTANCE_INFO.writeImpl login all-instance path.
		buffer.WriteC(_updateType);
		buffer.WriteD(_singleInstanceCooldownId);
		buffer.WriteC(0);
		buffer.WriteH(1);
		buffer.WriteD(_player.ObjectId);
		buffer.WriteH(_instanceCooltimes.Count);

		var nowMillis = _clock().ToUnixTimeMilliseconds();
		foreach (var cooltime in _instanceCooltimes)
		{
			_player.PortalCooldowns.TryGetValue(cooltime.WorldId, out var cooldown);
			buffer.WriteD(cooltime.Id);
			buffer.WriteD(0);
			buffer.WriteD(cooldown == null ? 0 : (int)Math.Max(0, (cooldown.ReuseTimeMillis - nowMillis) / 1000));
			buffer.WriteD(cooltime.MaxCount);
			buffer.WriteD(cooldown == null ? 0 : -cooldown.EntryCount);
			buffer.WriteC(IsHiddenForPlayer(cooltime.Race, _player.Race) ? 0 : 1);
		}

		buffer.WriteS(_player.Name);
	}

	private static bool IsHiddenForPlayer(string cooltimeRace, string playerRace)
	{
		// Java parity: InstanceCooltime.race == activePlayer.getOppositeRace() ? 0 : 1.
		var oppositeRace = playerRace.ToUpperInvariant() switch
		{
			"ELYOS" => "ASMODIANS",
			"ASMODIANS" => "ELYOS",
			_ => string.Empty,
		};
		return string.Equals(cooltimeRace, oppositeRace, StringComparison.OrdinalIgnoreCase);
	}
}
