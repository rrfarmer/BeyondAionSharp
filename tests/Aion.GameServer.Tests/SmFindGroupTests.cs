using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmFindGroupTests
{
	[Fact]
	public void PacketOpCode_MatchesJavaServerOpcode()
	{
		// Java parity: ServerPacketsOpcodes.addPacketOpcode(166, SM_FIND_GROUP.class).
		Assert.Equal(166, SmFindGroup.PacketOpCode);
	}

	[Fact]
	public void WritePayload_RemoveRecruitmentMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(SmFindGroup.RemoveRecruitment(700101, 5, 6, 7, 8));

		Assert.Equal(Convert.FromHexString("01C5AE0A0005060708"), payload);
	}

	[Fact]
	public void WritePayload_RemoveApplicationMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(SmFindGroup.RemoveApplication(700105));

		Assert.Equal(Convert.FromHexString("05C9AE0A00"), payload);
	}

	[Fact]
	public void WritePayload_EnableRegisterForInstancesMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(SmFindGroup.EnableRegisterForInstances([300110000, 300150000]));

		Assert.Equal(Convert.FromHexString("1A0200B050E311F0ECE311"), payload);
	}

	[Fact]
	public void WritePayload_InstanceApplicationWhisperMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.SendInstanceGroupApplicationAsWhisperChatMessage(
				new FindGroupInstanceApplicantSnapshot(0x01020304, ClassId: 5, Level: 65, Name: "Applicant")));

		Assert.Equal(
			Convert.FromHexString("0B04030201000000000000000000000005410000004100700070006C006900630061006E0074000000"),
			payload);
	}

	[Fact]
	public void WritePayload_ShowEnterButtonMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.ShowEnterButtonInPrepareForEntryWindow(new FindGroupInstanceGroupWindowSnapshot(0x01020304, 0x11223344)));

		Assert.Equal(Convert.FromHexString("120403020144332211"), payload);
	}

	[Fact]
	public void WritePayload_ShowPrepareWindowMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.ShowPrepareForEntryWindow(new FindGroupInstanceGroupWindowSnapshot(0x01020304, 0x11223344)));

		Assert.Equal(Convert.FromHexString("160403020144332211"), payload);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
