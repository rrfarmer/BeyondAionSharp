using Aion.Commons.Database;
using Aion.Commons.Network;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer.ServerPackets;

namespace Aion.LoginServer.Tests;

public sealed class BanTimestampParityTests
{
	[Theory]
	[InlineData(1_768_496_400L)] // 2026-01-15 12:00 America/New_York (UTC-05)
	[InlineData(1_784_131_200L)] // 2026-07-15 12:00 America/New_York (UTC-04)
	public void MacAndHddBan_DatabaseReloadPreservesJavaWireEpoch(long databaseEpochSeconds)
	{
		// The repositories persist with FROM_UNIXTIME(epoch) and reload with UNIX_TIMESTAMP(time).
		// Reconstructing from that DB value simulates an LS restart without a live MySQL dependency.
		var reloadedInstant = DatabaseTimestamp.FromUnixTimeSeconds(databaseEpochSeconds);
		var expectedEpochMilliseconds = checked(databaseEpochSeconds * 1000);

		var macPayload = new SmMacBanList(
			[new BannedMacEntry("aa-bb", reloadedInstant, "qa")]).SerializePayload();
		using var mac = new PacketBuffer(macPayload);
		Assert.Equal(9, mac.ReadC());
		Assert.Equal(1, mac.ReadD());
		Assert.Equal("aa-bb", mac.ReadS());
		Assert.Equal(expectedEpochMilliseconds, mac.ReadQ());
		Assert.Equal("qa", mac.ReadS());

		var hddPayload = new SmHddBanList(
			new Dictionary<string, DateTime> { ["disk"] = reloadedInstant }).SerializePayload();
		using var hdd = new PacketBuffer(hddPayload);
		Assert.Equal(10, hdd.ReadC());
		Assert.Equal(1, hdd.ReadD());
		Assert.Equal("disk", hdd.ReadS());
		Assert.Equal(expectedEpochMilliseconds, hdd.ReadQ());
	}

	[Fact]
	public void BanPackets_RejectUnspecifiedTimestampInsteadOfApplyingHostOffset()
	{
		var unspecified = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified);

		Assert.Throws<ArgumentException>(
			() => new SmMacBanList([new BannedMacEntry("aa-bb", unspecified, "qa")]).SerializePayload());
		Assert.Throws<ArgumentException>(
			() => new SmHddBanList(new Dictionary<string, DateTime> { ["disk"] = unspecified }).SerializePayload());
	}
}
