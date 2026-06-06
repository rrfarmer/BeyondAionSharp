using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmLegionTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaLegionOpcodeAsInGameOnly()
	{
		Assert.IsType<CmLegion>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_EditPermissionsReadsSignedShorts()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0D);
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8000);
		buffer.WriteH(0x7fff);
		buffer.WriteH(1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x0D, packet.ExOpcode);
		Assert.Equal((short)-1, packet.DeputyPermission);
		Assert.Equal(short.MinValue, packet.CenturionPermission);
		Assert.Equal(short.MaxValue, packet.LegionaryPermission);
		Assert.Equal((short)1, packet.VolunteerPermission);
	}

	[Fact]
	public void ReadFrom_RankBranchConsumesRankAndCharacterName()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x06);
		buffer.WriteD(3);
		buffer.WriteS("Lurion");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x06, packet.ExOpcode);
		Assert.Equal(3, packet.Rank);
		Assert.Equal("Lurion", packet.CharacterName);
	}

	[Fact]
	public void ReadFrom_KickBranchConsumesJavaEmptyIdAndCharacterName()
	{
		var packet = CreateKickMemberPacket("Lurion");

		Assert.Equal(0x04, packet.ExOpcode);
		Assert.Equal("Lurion", packet.CharacterName);
	}

	[Fact]
	public void ReadFrom_LeaveBranchConsumesJavaEmptyFields()
	{
		var packet = CreateLeavePacket();

		Assert.Equal(0x02, packet.ExOpcode);
	}

	[Fact]
	public void ReadFrom_RefreshInfoConsumesJavaEmptyFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x08);
		buffer.WriteD(0);
		buffer.WriteH(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x08, packet.ExOpcode);
	}

	[Fact]
	public void ReadFrom_ShowNoticeConsumesJavaEmptyFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x07);
		buffer.WriteD(0);
		buffer.WriteH(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x07, packet.ExOpcode);
	}

	[Fact]
	public void ReadFrom_ChangeSelfIntroConsumesJavaEmptyIdAndIntro()
	{
		var packet = CreateChangeSelfIntroPacket("Ready for sieges");

		Assert.Equal(0x0A, packet.ExOpcode);
		Assert.Equal("Ready for sieges", packet.NewSelfIntro);
	}

	[Fact]
	public void ReadFrom_ChangeNicknameConsumesMemberAndNicknameLikeJava()
	{
		var packet = CreateChangeNicknamePacket("tester", "Siege Lead");

		Assert.Equal(0x0F, packet.ExOpcode);
		Assert.Equal("tester", packet.CharacterName);
		Assert.Equal("Siege Lead", packet.NewNickname);
	}

	[Fact]
	public void SmSystemMessage_LegionNoticeHelpersUseJavaIdsAndParameters()
	{
		var noNotice = SmSystemMessage.MsgNoSetGuildNotice();
		Assert.Equal(1390127, noNotice.MessageId);
		Assert.Empty(noNotice.Parameters);

		var notice = SmSystemMessage.GuildNotice("Assemble", 1_771_234_500);
		Assert.Equal(1400019, notice.MessageId);
		Assert.Equal(["Assemble", "1771234500", "2"], notice.Parameters);

		Assert.Equal(1300276, SmSystemMessage.GuildWriteNoticeDontHaveRight().MessageId);
		Assert.Equal(1300277, SmSystemMessage.GuildWriteNoticeDone().MessageId);
		Assert.Equal(1390128, SmSystemMessage.MsgClearGuildNotice().MessageId);
		Assert.Equal(1300283, SmSystemMessage.GuildChangeRightDontHaveRight().MessageId);
		Assert.Equal(1300282, SmSystemMessage.GuildWriteIntroDone().MessageId);
		Assert.Equal(1300262, SmSystemMessage.GuildChangeMemberRankDontHaveRight().MessageId);
		Assert.Equal(1300263, SmSystemMessage.GuildChangeMemberRankErrorSelf().MessageId);
		Assert.Equal(1300264, SmSystemMessage.GuildChangeMemberRankNoUser().MessageId);
		var rankNotMember = SmSystemMessage.GuildChangeMemberRankHeIsNotMyGuildMember("Lurion");
		Assert.Equal(1300265, rankNotMember.MessageId);
		Assert.Equal(["Lurion"], rankNotMember.Parameters);
		Assert.Equal(1300237, SmSystemMessage.GuildLeaveCantLeaveGuildWhileUsingWarehouse().MessageId);
		Assert.Equal(1300238, SmSystemMessage.GuildLeaveMasterCantLeaveBeforeChangeMaster().MessageId);
		Assert.Equal(1300243, SmSystemMessage.GuildBanishCantBanishSelf().MessageId);
		Assert.Equal(1300244, SmSystemMessage.GuildBanishDontHaveRight().MessageId);
		var banishNotMember = SmSystemMessage.GuildBanishHeIsNotMyGuildMember("Lurion");
		Assert.Equal(1300248, banishNotMember.MessageId);
		Assert.Equal(["Lurion"], banishNotMember.Parameters);
		Assert.Equal(1300249, SmSystemMessage.GuildBanishCanBanishMaster().MessageId);
		Assert.Equal(1390241, SmSystemMessage.GuildBanishCanNotBanishSameMemberRank().MessageId);
		Assert.Equal(1300313, SmSystemMessage.GuildChangeMemberNicknameDontHaveRight().MessageId);
		var notMember = SmSystemMessage.GuildChangeMemberNicknameHeIsNotMyGuildMember("Lurion");
		Assert.Equal(1300314, notMember.MessageId);
		Assert.Equal(["Lurion"], notMember.Parameters);
	}

	[Fact]
	public void SmLegionInfo_WritesJavaPayloadWithCurrentRuntimeFields()
	{
		var packet = new SmLegionInfo(
			"Hydrated Legion",
			legionLevel: 4,
			rankingPosition: 123,
			deputyPermission: 1,
			centurionPermission: 2,
			legionaryPermission: 3,
			volunteerPermission: 4,
			contributionPoints: 55_000,
			disbandTime: 1_771_234_567,
			occupiedLegionDominion: 5,
			lastLegionDominion: 6,
			currentLegionDominion: 7,
			announcement: "Assemble",
			announcementTime: 1_771_234_500);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(123, reader.ReadD());
		Assert.Equal(1, reader.ReadSignedH());
		Assert.Equal(2, reader.ReadSignedH());
		Assert.Equal(3, reader.ReadSignedH());
		Assert.Equal(4, reader.ReadSignedH());
		Assert.Equal(55_000, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal("Assemble", reader.ReadS());
		Assert.Equal(1_771_234_500, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public void SmLegionInfo_FromPlayerWritesLoadedRuntimeFieldsLikeJava()
	{
		var player = CreateLegionPlayer();
		player.LegionAnnouncement = "Assemble";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(SmLegionInfo.FromPlayer(player)));

		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(11, reader.ReadSignedH());
		Assert.Equal(12, reader.ReadSignedH());
		Assert.Equal(13, reader.ReadSignedH());
		Assert.Equal(14, reader.ReadSignedH());
		Assert.Equal(55_000, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal("Assemble", reader.ReadS());
		Assert.Equal(1_771_234_500, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_RefreshInfoSendsActivePlayerLegionInfoLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateRefreshInfoPacket());

		var response = Assert.IsType<SmLegionInfo>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(11, reader.ReadSignedH());
		Assert.Equal(12, reader.ReadSignedH());
		Assert.Equal(13, reader.ReadSignedH());
		Assert.Equal(14, reader.ReadSignedH());
		Assert.Equal(55_000, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ShowNoticeSendsNoNoticeMessageWhenAnnouncementMissingLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateShowNoticePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390127, response.MessageId);
		Assert.Empty(response.Parameters);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ShowNoticeSendsLoadedAnnouncementLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		player.LegionAnnouncement = "Assemble";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateShowNoticePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1400019, response.MessageId);
		Assert.Equal(["Assemble", "1771234500", "2"], response.Parameters);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_RefreshInfoSkipsPlayerWithoutLegionLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, new Player { ObjectId = 1001, Name = "Unguilded" });

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateRefreshInfoPacket());

		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EditPermissionsWithoutBrigadeGeneralSendsNoRightLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateEditPermissionsPacket(21, 22, 23, 24));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300283, response.MessageId);
		Assert.Equal(11, player.LegionDeputyPermission);
		Assert.Equal(12, player.LegionCenturionPermission);
		Assert.Equal(13, player.LegionLegionaryPermission);
		Assert.Equal(14, player.LegionVolunteerPermission);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EditPermissionsMutatesRuntimeStateAndSendsEditLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateEditPermissionsPacket(21, 22, 23, 24));

		Assert.Equal(21, player.LegionDeputyPermission);
		Assert.Equal(22, player.LegionCenturionPermission);
		Assert.Equal(23, player.LegionLegionaryPermission);
		Assert.Equal(24, player.LegionVolunteerPermission);

		var response = Assert.IsType<SmLegionEdit>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(0x02, reader.ReadC());
		Assert.Equal(21, reader.ReadSignedH());
		Assert.Equal(22, reader.ReadSignedH());
		Assert.Equal(23, reader.ReadSignedH());
		Assert.Equal(24, reader.ReadSignedH());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeSelfIntroInvalidValueReturnsWithoutMutationLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		player.LegionSelfIntro = "Old intro";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeSelfIntroPacket(string.Empty));

		Assert.Empty(pair.SentPackets);
		Assert.Equal("Old intro", player.LegionSelfIntro);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeSelfIntroMutatesRuntimeStateAndSendsPacketsLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeSelfIntroPacket("Ready for sieges"));

		Assert.Equal("Ready for sieges", player.LegionSelfIntro);
		Assert.Collection(
			pair.SentPackets,
			packet =>
			{
				var response = Assert.IsType<SmLegionUpdateSelfIntro>(packet);
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
				Assert.Equal(player.ObjectId, reader.ReadD());
				Assert.Equal("Ready for sieges", reader.ReadS());
			},
			packet =>
			{
				var response = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300282, response.MessageId);
			});
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameMutatesActiveMemberAndSendsUpdateLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionNickname = "Old";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("tester", "Siege Lead"));

		Assert.Equal("Siege Lead", player.LegionNickname);
		Assert.Equal(0, repository.LoadLegionMemberByNameCalls);
		Assert.Equal(0, repository.SaveLegionMemberNicknameCalls);
		AssertLegionUpdateNicknamePacket(Assert.Single(pair.SentPackets), player.ObjectId, "Siege Lead");
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknamePersistsOfflineMemberAndSendsUpdateLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = new LegionMemberSnapshot(
				2002,
				77,
				"Lurion",
				LegionRanks.Centurion,
				string.Empty,
				string.Empty,
				IsOnline: false),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("lurion", "Scout"));

		Assert.Equal(1, repository.LoadLegionMemberByNameCalls);
		Assert.Equal((77, "Lurion"), repository.LoadedLegionMemberByNameRequest);
		Assert.Equal(1, repository.SaveLegionMemberNicknameCalls);
		Assert.Equal((2002, "Scout"), repository.SavedLegionMemberNickname);
		AssertLegionUpdateNicknamePacket(Assert.Single(pair.SentPackets), 2002, "Scout");
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameWithoutBrigadeGeneralSendsNoRightLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		player.LegionNickname = "Old";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("Tester", "Scout"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300313, response.MessageId);
		Assert.Equal("Old", player.LegionNickname);
		Assert.Equal(0, repository.SaveLegionMemberNicknameCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameMissingMemberSendsNotMyGuildMemberLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("missing", "Scout"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300314, response.MessageId);
		Assert.Equal(["Missing"], response.Parameters);
		Assert.Equal((77, "Missing"), repository.LoadedLegionMemberByNameRequest);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameInvalidValueReturnsWithoutMutationLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionNickname = "Old";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("Tester", string.Empty));

		Assert.Empty(pair.SentPackets);
		Assert.Equal("Old", player.LegionNickname);
		Assert.Equal(0, repository.SaveLegionMemberNicknameCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankWithoutBrigadeGeneralSendsNoRightLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 2, "Lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300262, response.MessageId);
		Assert.Equal(0, repository.LoadLegionMemberByNameCalls);
		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankMissingMemberSendsNoUserLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 2, "missing"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300264, response.MessageId);
		Assert.Equal((77, "Missing"), repository.LoadedLegionMemberByNameRequest);
		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankOtherLegionMemberSendsNotMyGuildMemberLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = new LegionMemberSnapshot(
				2004,
				88,
				"Outsider",
				LegionRanks.Legionary,
				string.Empty,
				string.Empty,
				IsOnline: false),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 2, "outsider"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300265, response.MessageId);
		Assert.Equal(["Outsider"], response.Parameters);
		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankRejectsSelfLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 2, "tester"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300263, response.MessageId);
		Assert.Equal(LegionRanks.BrigadeGeneral, player.LegionRank);
		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankPersistsOfflineMemberAndSendsUpdateLikeJava()
	{
		var lastOnline = new DateTime(2026, 06, 01, 12, 30, 00, DateTimeKind.Utc);
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = new LegionMemberSnapshot(
				2002,
				77,
				"Lurion",
				LegionRanks.Legionary,
				string.Empty,
				string.Empty,
				IsOnline: false,
				PlayerClass: "CLERIC",
				Exp: 0,
				WorldId: 210010000,
				LastOnline: lastOnline),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 2, "lurion"));

		Assert.Equal(1, repository.LoadLegionMemberByNameCalls);
		Assert.Equal((77, "Lurion"), repository.LoadedLegionMemberByNameRequest);
		Assert.Equal(1, repository.SaveLegionMemberRankCalls);
		Assert.Equal((2002, LegionRanks.Centurion), repository.SavedLegionMemberRank);
		AssertLegionUpdateMemberPacket(
			Assert.Single(pair.SentPackets),
			playerObjectId: 2002,
			rankId: 2,
			classId: 10,
			level: 1,
			worldId: 210010000,
			online: false,
			lastOnlineEpochSeconds: (int)new DateTimeOffset(lastOnline).ToUnixTimeSeconds(),
			gameServerId: 1,
			messageId: 1300267,
			text: "Lurion");
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankOnlineMemberSendsUpdateWithoutPersistenceLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = new LegionMemberSnapshot(
				2003,
				77,
				"Serin",
				LegionRanks.Volunteer,
				string.Empty,
				string.Empty,
				IsOnline: true,
				PlayerClass: "RANGER",
				Exp: 0,
				WorldId: 220010000),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 1, "serin"));

		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
		AssertLegionUpdateMemberPacket(
			Assert.Single(pair.SentPackets),
			playerObjectId: 2003,
			rankId: 1,
			classId: 5,
			level: 1,
			worldId: 220010000,
			online: true,
			lastOnlineEpochSeconds: 0,
			gameServerId: 1,
			messageId: 1400902,
			text: "Serin");
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickMissingMemberSendsNotMyGuildMemberLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("missing"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300248, response.MessageId);
		Assert.Equal(["Missing"], response.Parameters);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickRejectsSelfLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("tester"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300243, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickRejectsBrigadeGeneralTargetLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = CreateMemberSnapshot(2002, "Lurion", LegionRanks.BrigadeGeneral),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300249, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickRejectsEqualOrHigherRankLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = CreateMemberSnapshot(2002, "Lurion", LegionRanks.Deputy),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		player.LegionDeputyPermission = 0x10;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390241, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickWithoutPermissionSendsNoRightLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = CreateMemberSnapshot(2002, "Lurion", LegionRanks.Centurion),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		player.LegionDeputyPermission = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300244, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickDeletesMemberAddsHistoryAndSendsLeavePacketLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = CreateMemberSnapshot(2002, "Lurion", LegionRanks.Legionary),
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("lurion"));

		Assert.Equal(1, repository.DeleteLegionMemberCalls);
		Assert.Equal(2002, repository.DeletedLegionMemberObjectId);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.Kick, "Lurion", string.Empty), repository.InsertedLegionHistory);
		AssertLegionLeaveMemberPacket(
			Assert.Single(pair.SentPackets),
			playerObjectId: 2002,
			messageId: 1300247,
			name: "Tester",
			name1: "Lurion");
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_KickResetsResolvedOnlineTargetAndSendsDirectDonePacketLikeJava()
	{
		var target = new Player
		{
			ObjectId = 2005,
			Name = "Lurion",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionLevel = 4,
			LegionRank = LegionRanks.Legionary,
			LegionNickname = "Scout",
			LegionSelfIntro = "Ready",
			LegionContributionPoints = 55_000,
			LegionDeputyPermission = 11,
			LegionCenturionPermission = 12,
			LegionLegionaryPermission = 13,
			LegionVolunteerPermission = 14,
		};
		var bystander = CreateLegionPlayer(3003, "Watcher");
		bystander.LegionRank = LegionRanks.Centurion;
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 99;
		outsider.LegionName = "Other Legion";
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMemberByName = CreateMemberSnapshot(target.ObjectId, target.Name, LegionRanks.Legionary, isOnline: true),
		};
		var registry = new CapturingConnectionRegistry(target, bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateKickMemberPacket("lurion"));

		Assert.Equal(0, target.LegionId);
		Assert.Equal(string.Empty, target.LegionName);
		Assert.Equal(string.Empty, target.LegionRank);
		Assert.Equal(string.Empty, target.LegionNickname);
		Assert.Equal(string.Empty, target.LegionSelfIntro);
		AssertLegionLeaveMemberPacket(Assert.Single(pair.SentPackets), target.ObjectId, 1300247, "Tester", "Lurion");
		Assert.Equal(2, registry.DirectPackets.Count);
		var bystanderPacket = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionLeaveMemberPacket(bystanderPacket.Packet, target.ObjectId, 1300247, "Tester", "Lurion");
		var targetPacket = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == target.ObjectId);
		AssertLegionLeaveMemberPacket(targetPacket.Packet, 0, 1300246, "Hydrated Legion", string.Empty);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LeaveRejectsBrigadeGeneralLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLeavePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300238, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
		Assert.Equal(77, player.LegionId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LeaveRejectsCurrentWarehouseUserLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var runtimeContext = new GameServerRuntimeContext();
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Legionary;
		Assert.True(runtimeContext.LegionWarehouses.TrySetInUse(player.LegionId, player.ObjectId));
		await using var pair = await TestConnectionPair.CreateAsync(repository, runtimeContext: runtimeContext);
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLeavePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300237, response.MessageId);
		Assert.Equal(0, repository.DeleteLegionMemberCalls);
		Assert.Equal(player.ObjectId, runtimeContext.LegionWarehouses.GetCurrentUser(77));
		Assert.Equal(77, player.LegionId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LeaveDeletesMemberAddsKickHistoryResetsPlayerAndSendsDonePacketLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var runtimeContext = new GameServerRuntimeContext();
		var bystander = CreateLegionPlayer(2002, "Watcher");
		bystander.LegionRank = LegionRanks.Centurion;
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 99;
		outsider.LegionName = "Other Legion";
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Legionary;
		Assert.True(runtimeContext.LegionWarehouses.TrySetInUse(player.LegionId, 3003));
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry, runtimeContext);
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLeavePacket());

		Assert.Equal(1, repository.DeleteLegionMemberCalls);
		Assert.Equal(player.ObjectId, repository.DeletedLegionMemberObjectId);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.Kick, "Tester", string.Empty), repository.InsertedLegionHistory);
		Assert.Equal(0, player.LegionId);
		Assert.Equal(string.Empty, player.LegionName);
		Assert.Equal(string.Empty, player.LegionRank);
		Assert.Equal(3003, runtimeContext.LegionWarehouses.GetCurrentUser(77));
		var bystanderPacket = Assert.Single(registry.DirectPackets);
		Assert.Equal(bystander.ObjectId, bystanderPacket.PlayerObjectId);
		AssertLegionLeaveMemberPacket(bystanderPacket.Packet, 1001, 1300240, "Tester", "Hydrated Legion");
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
		AssertLegionLeaveMemberPacket(
			Assert.Single(pair.SentPackets),
			playerObjectId: 0,
			messageId: 1300241,
			name: "Hydrated Legion",
			name1: string.Empty);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LeaveDeleteFailureDoesNotResetOrSendLikeJavaAbort()
	{
		var repository = new EmptyPlayerEnterWorldRepository { DeleteLegionMemberResult = false };
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Legionary;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLeavePacket());

		Assert.Equal(1, repository.DeleteLegionMemberCalls);
		Assert.Equal(0, repository.InsertLegionHistoryCalls);
		Assert.Equal(77, player.LegionId);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public void ReadFrom_ChangeAnnouncementReadsJavaMessage()
	{
		var packet = CreateChangeAnnouncementPacket("New notice");

		Assert.Equal(0x09, packet.ExOpcode);
		Assert.Equal("New notice", packet.Announcement);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeAnnouncementWithoutEditRightSendsNoRightLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Volunteer;
		player.LegionVolunteerPermission = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket("New notice"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300276, response.MessageId);
		Assert.Equal(string.Empty, player.LegionAnnouncement);
		Assert.Equal(0, repository.SaveLegionAnnouncementCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeAnnouncementPersistsRuntimeStateAndSuccessLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket("New notice"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300277, response.MessageId);
		Assert.Equal("New notice", player.LegionAnnouncement);
		Assert.True(player.LegionAnnouncementEpochSeconds > 0);
		Assert.Equal(1, repository.SaveLegionAnnouncementCalls);
		Assert.NotNull(repository.SavedLegionAnnouncement);
		Assert.Equal(player.LegionId, repository.SavedLegionAnnouncement.Value.LegionId);
		Assert.Equal("New notice", repository.SavedLegionAnnouncement.Value.Announcement);
		Assert.NotNull(repository.SavedLegionAnnouncement.Value.AnnouncementTime);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeAnnouncementTruncatesLongMessageLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);
		var longNotice = new string('A', 300);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket(longNotice));

		Assert.Equal(256, player.LegionAnnouncement.Length);
		Assert.Equal(new string('A', 256), player.LegionAnnouncement);
		Assert.NotNull(repository.SavedLegionAnnouncement);
		Assert.Equal(new string('A', 256), repository.SavedLegionAnnouncement.Value.Announcement);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ClearAnnouncementPersistsNullAndSendsClearLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionAnnouncement = "Old notice";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket(string.Empty));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390128, response.MessageId);
		Assert.Equal(string.Empty, player.LegionAnnouncement);
		Assert.Equal(0, player.LegionAnnouncementEpochSeconds);
		Assert.Equal(1, repository.SaveLegionAnnouncementCalls);
		Assert.NotNull(repository.SavedLegionAnnouncement);
		Assert.Equal(player.LegionId, repository.SavedLegionAnnouncement.Value.LegionId);
		Assert.Null(repository.SavedLegionAnnouncement.Value.Announcement);
		Assert.Null(repository.SavedLegionAnnouncement.Value.AnnouncementTime);
	}

	private static CmLegion CreatePacket()
	{
		return new CmLegion(45, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static CmLegion CreateShowNoticePacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x07);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateChangeAnnouncementPacket(string announcement)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x09);
		buffer.WriteD(0);
		buffer.WriteS(announcement);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateChangeSelfIntroPacket(string selfIntro)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0A);
		buffer.WriteD(0);
		buffer.WriteS(selfIntro);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateChangeNicknamePacket(string memberName, string nickname)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0F);
		buffer.WriteS(memberName);
		buffer.WriteS(nickname);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateChangeRankPacket(int rankId, string memberName)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x06);
		buffer.WriteD(rankId);
		buffer.WriteS(memberName);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateLeavePacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x02);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateKickMemberPacket(string memberName)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x04);
		buffer.WriteD(0);
		buffer.WriteS(memberName);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateRefreshInfoPacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x08);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateEditPermissionsPacket(short deputy, short centurion, short legionary, short volunteer)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0D);
		buffer.WriteH(deputy);
		buffer.WriteH(centurion);
		buffer.WriteH(legionary);
		buffer.WriteH(volunteer);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static Player CreateLegionPlayer(int objectId = 1001, string name = "Tester")
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionLevel = 4,
			LegionDisbandTime = 1_771_234_567,
			LegionContributionPoints = 55_000,
			LegionOccupiedLegionDominion = 5,
			LegionLastLegionDominion = 6,
			LegionCurrentLegionDominion = 7,
			LegionDeputyPermission = 11,
			LegionCenturionPermission = 12,
			LegionLegionaryPermission = 13,
			LegionVolunteerPermission = 14,
		};
	}

	private static Player CreateBrigadeGeneralPlayer()
	{
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.BrigadeGeneral;
		return player;
	}

	private static LegionMemberSnapshot CreateMemberSnapshot(
		int playerObjectId,
		string name,
		string rank,
		int legionId = 77,
		bool isOnline = false)
	{
		return new LegionMemberSnapshot(
			playerObjectId,
			legionId,
			name,
			rank,
			string.Empty,
			string.Empty,
			isOnline);
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();

		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static void AssertLegionUpdateNicknamePacket(GameServerPacket packet, int playerObjectId, string nickname)
	{
		var response = Assert.IsType<SmLegionUpdateNickname>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(nickname, reader.ReadS());
	}

	private static void AssertLegionUpdateMemberPacket(
		GameServerPacket packet,
		int playerObjectId,
		int rankId,
		int classId,
		int level,
		int worldId,
		bool online,
		int lastOnlineEpochSeconds,
		int gameServerId,
		int messageId,
		string text)
	{
		var response = Assert.IsType<SmLegionUpdateMember>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(rankId, reader.ReadC());
		Assert.Equal(classId, reader.ReadC());
		Assert.Equal(level, reader.ReadC());
		Assert.Equal(worldId, reader.ReadD());
		Assert.Equal(online ? 1 : 0, reader.ReadC());
		Assert.Equal(lastOnlineEpochSeconds, reader.ReadD());
		Assert.Equal(gameServerId, reader.ReadD());
		Assert.Equal(messageId, reader.ReadD());
		Assert.Equal(text, reader.ReadS());
	}

	private static void AssertLegionLeaveMemberPacket(
		GameServerPacket packet,
		int playerObjectId,
		int messageId,
		string name,
		string name1)
	{
		var response = Assert.IsType<SmLegionLeaveMember>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(messageId, reader.ReadD());
		Assert.Equal(name, reader.ReadS());
		Assert.Equal(name1, reader.ReadS());
	}

	private static async Task InvokeHandleInfrastructurePacketAsync(GameServerConnection connection, GameClientPacket packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleInfrastructurePacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = (Task)method.Invoke(connection, [packet])!;
		await task;
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }
		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			IPlayerEnterWorldRepository? playerEnterWorldRepository = null,
			IGameClientConnectionRegistry? connectionRegistry = null,
			GameServerRuntimeContext? runtimeContext = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"legion-info-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					runtimeContext: runtimeContext,
					playerEnterWorldRepository: playerEnterWorldRepository,
					connectionRegistry: connectionRegistry,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TestConnectionPair(client, connection, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyList<Player> _players;

		public CapturingConnectionRegistry(params Player[] players)
		{
			_players = players;
		}

		public List<(int PlayerObjectId, GameServerPacket Packet)> DirectPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = _players.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.Ordinal));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in _players)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			DirectPackets.Add((playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
