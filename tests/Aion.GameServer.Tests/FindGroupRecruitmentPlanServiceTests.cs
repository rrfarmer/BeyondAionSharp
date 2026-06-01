using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupRecruitmentPlanServiceTests
{
	[Fact]
	public void AddRecruitment_StoresSoloEntryThenPlansPostedMessageAndRaceFilteredShowList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var asmodian = CreatePlayer(1001, "OtherRace", "ASMODIANS", "RANGER", 45);
		service.AddRecruitment(asmodian, "Asmo only", groupType: 3, nowEpochSeconds: 111);
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);

		var plan = service.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 222);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, plan.Status);
		Assert.NotNull(plan.CurrentRecruitment);
		Assert.Equal(2001, plan.CurrentRecruitment.ObjectId);
		Assert.True(plan.CurrentRecruitment.IsSoloPlayer);
		Assert.Equal("ELYOS", plan.CurrentRecruitment.Race);
		Assert.Equal("Recruiter", plan.CurrentRecruitment.RecruiterName);
		Assert.Equal(1, plan.CurrentRecruitment.Size);
		Assert.Equal(50, plan.CurrentRecruitment.MinLevel);
		Assert.Equal(50, plan.CurrentRecruitment.MaxLevel);
		Assert.Equal(1, plan.CurrentRecruitment.ClassId);
		Assert.Equal(222, plan.CurrentRecruitment.LastUpdate);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(2001, direct.RecipientObjectId);
		Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED", direct.JavaSource);
		Assert.Equal(1400392, Assert.IsType<SmSystemMessage>(direct.Packet).MessageId);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.NotNull(plan.ShowRecruitmentsPlan);
		var show = plan.ShowRecruitmentsPlan!;
		Assert.Equal("ELYOS", show.Race);
		Assert.Equal(222, show.LastUpdate);
		var snapshot = Assert.Single(show.Recruitments);
		Assert.Equal(2001, snapshot.ObjectId);
		Assert.Equal("Need healer", snapshot.Message);
		Assert.Equal("Recruiter", snapshot.RecruiterName);
		Assert.Equal(2, snapshot.GroupType);
		Assert.Equal(1, snapshot.Size);
		Assert.Equal(50, snapshot.MinLevel);
		Assert.Equal(50, snapshot.MaxLevel);
		Assert.Equal(222, snapshot.LastUpdate);
		Assert.Equal(
			Convert.FromHexString("0001000100DE000000D107000000000010024E0065006500640020006800650061006C006500720000005200650063007200750069007400650072000000013232DE000000"),
			SerializeUnencryptedPayload(show.Packet));
	}

	[Fact]
	public void AddRecruitment_UsesTeamSubjectWhenCurrentTeamExists()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(2001, "Leader", "ELYOS", "RANGER", 55), PlayerTeamMembership.Group, teamId: 9001);
		var team = new FindGroupRecruitmentSubject(
			ObjectId: 9001,
			Race: "ELYOS",
			IsSoloPlayer: false,
			RecruiterName: "Leader",
			Size: 4,
			MinLevel: 50,
			MaxLevel: 55,
			ClassId: 5);

		var plan = service.AddRecruitment(player, "Group run", groupType: 4, nowEpochSeconds: 333, team);

		Assert.NotNull(plan.CurrentRecruitment);
		var state = plan.CurrentRecruitment!;
		Assert.Equal(9001, state.ObjectId);
		Assert.False(state.IsSoloPlayer);
		Assert.Equal(4, state.Size);
		Assert.Equal(50, state.MinLevel);
		Assert.Equal(55, state.MaxLevel);
		Assert.NotNull(plan.ShowRecruitmentsPlan);
		var snapshot = Assert.Single(plan.ShowRecruitmentsPlan!.Recruitments);
		Assert.Equal(9001, snapshot.ObjectId);
		Assert.False(snapshot.IsSoloPlayer);
		Assert.Equal(4, snapshot.Size);
		Assert.Equal(50, snapshot.MinLevel);
		Assert.Equal(55, snapshot.MaxLevel);
	}

	[Fact]
	public void UpdateRecruitment_ExistingEntryMutatesMessageTypeAndTimestampWithoutPackets()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);
		service.AddRecruitment(player, "Old", groupType: 1, nowEpochSeconds: 100);

		var plan = service.UpdateRecruitment(player, "New", groupType: 6, nowEpochSeconds: 300);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Updated, plan.Status);
		Assert.NotNull(plan.CurrentRecruitment);
		var state = plan.CurrentRecruitment!;
		Assert.Equal("New", state.Message);
		Assert.Equal(6, state.GroupType);
		Assert.Equal(300, state.LastUpdate);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Null(plan.ShowRecruitmentsPlan);
		var show = service.ShowRecruitments("ELYOS", nowEpochSeconds: 301);
		Assert.Equal("New", Assert.Single(show.Recruitments).Message);
	}

	[Fact]
	public void RemoveRecruitment_MissingEntryDoesNotBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();

		var plan = service.RemoveRecruitment(404, serverId: 5, unknown1: 6, unknown2: 7, unknown3: 8);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.Status);
		Assert.Null(plan.RemovedRecruitment);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.ShowRecruitmentsPlan);
	}

	[Fact]
	public void RemoveRecruitment_ExistingEntryPlansRaceFilteredWorldBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);
		service.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 222);

		var plan = service.RemoveRecruitment(player, serverId: 5, unknown1: 6, unknown2: 7, unknown3: 8);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.Status);
		Assert.NotNull(plan.RemovedRecruitment);
		Assert.Equal(2001, plan.RemovedRecruitment!.ObjectId);
		Assert.NotNull(plan.WorldBroadcastIntent);
		var broadcast = plan.WorldBroadcastIntent!;
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())", broadcast.JavaSource);
		Assert.Equal(Convert.FromHexString("01D107000005060708"), SerializeUnencryptedPayload(broadcast.Packet));
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	private static Player CreatePlayer(int objectId, string name, string race, string playerClass, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = playerClass,
			Level = level,
		};
	}

	private static Player WithTeam(Player player, PlayerTeamMembership membership, int teamId)
	{
		player.TeamMembership = membership;
		player.CurrentTeamId = teamId;
		return player;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
