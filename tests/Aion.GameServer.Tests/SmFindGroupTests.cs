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
	public void WritePayload_ShowRecruitmentsMatchesJavaShape()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.ShowRecruitments(
				0x01020305,
				[
					new FindGroupRecruitmentSnapshot(
						ObjectId: 0x01020304,
						ServerId: 1,
						IsSoloPlayer: true,
						GroupType: 2,
						Message: "LFG",
						RecruiterName: "Recruiter",
						Size: 1,
						MinLevel: 65,
						MaxLevel: 65,
						LastUpdate: 0x01020305)
				]));

		Assert.Equal(
			Convert.FromHexString("0001000100050302010403020101000010024C00460047000000520065006300720075006900740065007200000001414105030201"),
			payload);
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

	[Fact]
	public void WritePayload_ShowInstanceGroupsMatchesJavaShape()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.ShowInstanceGroups(
				0x01020305,
				[
					new FindGroupInstanceGroupRegistrationSnapshot(
						GroupEntryId: 0x01020304,
						InstanceMaskId: 0x11223344,
						MemberCount: 1,
						MinMembers: 3,
						RecruiterObjectId: 0x01020304,
						MinLevel: 65,
						MaxLevel: 65,
						LastUpdate: 0x01020305,
						RecruiterName: "Recruiter",
						Message: "Entry")
				]));

		Assert.Equal(
			Convert.FromHexString(
				"0A010001000503020104030201443322110100000001030000040302010100000000000000414100000503020100000000520065006300720075006900740065007200000045006E007400720079000000"),
			payload);
	}

	[Fact]
	public void WritePayload_RegisterInstanceGroupMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.RegisterInstanceGroup(
			[
				new FindGroupInstanceGroupRegistrationSnapshot(
					GroupEntryId: 0x01020304,
					InstanceMaskId: 0x11223344,
					MemberCount: 1,
					MinMembers: 3,
					RecruiterObjectId: 0x01020304,
					MinLevel: 65,
					MaxLevel: 65,
					LastUpdate: 0x01020305,
					RecruiterName: "Recruiter",
					Message: "Entry")
			]));

		Assert.Equal(
			Convert.FromHexString(
				"0E0104030201443322110100000001030000040302010100010000000000414100000503020100000000520065006300720075006900740065007200000045006E007400720079000000"),
			payload);
	}

	[Fact]
	public void WritePayload_ShowInstanceGroupMemberInfoMatchesJavaShape()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.ShowInstanceGroupMemberInfo(
				new FindGroupInstanceGroupMemberInfoSnapshot(
					LastUpdate: 0x01020305,
					Members:
					[
						new FindGroupInstanceGroupMemberInfoMemberSnapshot(
							WorldId: 300110000,
							PlayerObjectId: 0x01020304,
							Level: 65,
							ClassId: 1,
							Name: "Recruiter")
					])));

		Assert.Equal(
			Convert.FromHexString("10010001000503020100000000B050E311040302014100000001000000010000005200650063007200750069007400650072000000"),
			payload);
	}

	[Fact]
	public void WritePayload_DestroyPrepareWindowMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.DestroyPrepareForEntryWindow(
				new FindGroupInstanceGroupWindowSnapshot(0x01020304, 0x11223344),
				showEnterInstanceMessage: false));

		Assert.Equal(Convert.FromHexString("17040302014433221100"), payload);
	}

	[Fact]
	public void WritePayload_UpdatePrepareWindowMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(
			SmFindGroup.UpdatePrepareForEntryWindow(
				new FindGroupInstanceGroupPrepareWindowSnapshot(
					GroupEntryId: 0x01020304,
					InstanceMaskId: 0x11223344,
					Members:
					[
						new FindGroupInstanceGroupPrepareMemberSnapshot(
							PlayerObjectId: 0x01020304,
							Level: 65,
							ClassId: 1,
							IsOnline: false,
							Name: "Recruiter")
					])));

		Assert.Equal(
			Convert.FromHexString("180403020144332211010000000000000000040302014100000001000000000001005200650063007200750069007400650072000000"),
			payload);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
