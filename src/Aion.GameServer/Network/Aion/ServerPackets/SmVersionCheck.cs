using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmVersionCheck : GameServerPacket
{
	public const int PacketOpCode = 0;
	public const int InternalVersion = 207;
	private static readonly DateTimeOffset ProcessStartTime = DateTimeOffset.UtcNow;
	private readonly SmVersionCheckRuntimeOptions _runtimeOptions;

	public SmVersionCheck(
		int version,
		EventTheme cityDecoration,
		GameServerOptions? options = null,
		Func<DateTimeOffset>? clock = null,
		DateTimeOffset? serverStartTime = null)
		: base(PacketOpCode)
	{
		Version = version;
		CityDecoration = cityDecoration;
		_runtimeOptions = SmVersionCheckRuntimeOptions.FromOptions(
			options ?? new GameServerOptions(),
			clock ?? (() => DateTimeOffset.UtcNow),
			serverStartTime ?? ProcessStartTime);
	}

	public int Version { get; }
	public EventTheme CityDecoration { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_VERSION_CHECK.writeImpl incompatible-client branch.
		if (Version != InternalVersion)
		{
			buffer.WriteC(1);
			return;
		}

		// Java parity: network/aion/serverpackets/SM_VERSION_CHECK.writeImpl compatible-client branch.
		buffer.WriteC(0);
		buffer.WriteC(_runtimeOptions.GameServerId);
		buffer.WriteD(150602);
		buffer.WriteD(150326);
		buffer.WriteD(0);
		buffer.WriteD(150317);
		buffer.WriteD(_runtimeOptions.ServerStartEpochSeconds);
		buffer.WriteC(0);
		buffer.WriteC(_runtimeOptions.ServerCountryCode);
		buffer.WriteC(0);
		buffer.WriteC(_runtimeOptions.ServerFlag);
		buffer.WriteD(_runtimeOptions.PacketGenerationEpochSeconds);
		buffer.WriteH(_runtimeOptions.MinimumSkillCastIntervalMillis);
		buffer.WriteC(1);
		buffer.WriteC(10);
		buffer.WriteC(1);
		buffer.WriteC(10);
		buffer.WriteC(_runtimeOptions.ChatServerMinLevel);
		buffer.WriteC(20);
		buffer.WriteC(20);
		buffer.WriteC(1);
		buffer.WriteH(2);
		buffer.WriteC(_runtimeOptions.CharacterReentryTimeSeconds);
		buffer.WriteD(CityDecoration.GetId());
		buffer.WriteC(0);
		buffer.WriteD(_runtimeOptions.StandardOffsetSecondsFromUtc);
		buffer.WriteC(0x04);
		buffer.WriteD(40014200);
		buffer.WriteC(1);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteH(3000);
		buffer.WriteH(1);
		buffer.WriteC(0);
		buffer.WriteC(1);
		buffer.WriteD(_runtimeOptions.DaylightSavingsSecondsFromUtc);
		buffer.WriteC(1);
		buffer.WriteC(1);
		buffer.WriteD(0);
		buffer.WriteC(0);
		buffer.WriteC(_runtimeOptions.AtreianPassportDisabled ? 1 : 0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteD(_runtimeOptions.ItemWrapLimit);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteD(1000);
		buffer.WriteC(0);
		buffer.WriteF(3.0f);
		buffer.WriteH(0);
	}
}

internal sealed record SmVersionCheckRuntimeOptions(
	int GameServerId,
	int ServerCountryCode,
	int ServerFlag,
	int PacketGenerationEpochSeconds,
	int ServerStartEpochSeconds,
	int MinimumSkillCastIntervalMillis,
	int ChatServerMinLevel,
	int CharacterReentryTimeSeconds,
	int StandardOffsetSecondsFromUtc,
	int DaylightSavingsSecondsFromUtc,
	bool AtreianPassportDisabled,
	int ItemWrapLimit)
{
	public static SmVersionCheckRuntimeOptions FromOptions(
		GameServerOptions options,
		Func<DateTimeOffset> clock,
		DateTimeOffset serverStartTime)
	{
		var now = clock();
		var timeZone = options.Core.GetTimeZone();
		var standardOffset = -(int)timeZone.BaseUtcOffset.TotalSeconds;
		var daylightSavings = timeZone.IsDaylightSavingTime(now.DateTime)
			? -(int)(timeZone.GetUtcOffset(now.DateTime) - timeZone.BaseUtcOffset).TotalSeconds
			: 0;
		var characterLimitCount = options.Core.CharacterLimitCount;
		var limitFactionMode = options.Core.CharacterFactionLimitationMode;
		var serverFlag = (characterLimitCount * 0x10) | (limitFactionMode * 4) | options.Core.CharacterCreationMode;

		return new SmVersionCheckRuntimeOptions(
			ClampByte(options.Network.GameServerId),
			ClampByte(options.Core.ServerCountryCode),
			ClampByte(serverFlag),
			unchecked((int)now.ToUnixTimeSeconds()),
			unchecked((int)serverStartTime.ToUnixTimeSeconds()),
			options.Core.MinimumSkillCastIntervalMillis,
			ClampByte(options.Core.ChatServerMinLevel),
			ClampByte(options.Core.CharacterReentryTimeSeconds),
			standardOffset,
			daylightSavings,
			AtreianPassportDisabled: false,
			options.Core.ItemWrapLimit);
	}

	private static int ClampByte(int value)
	{
		return Math.Clamp(value, 0, byte.MaxValue);
	}
}
