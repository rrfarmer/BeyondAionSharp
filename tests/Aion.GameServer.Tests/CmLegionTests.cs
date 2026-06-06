using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
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
	public void TryCreatePacket_RegistersJavaLegionDominionRankingOpcodeAsInGameOnly()
	{
		Assert.IsType<CmLegionDominionRequestRanking>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(29, buffer => buffer.WriteD(5)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(29, buffer => buffer.WriteD(5)),
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
	public void ReadFrom_InviteBranchConsumesJavaEmptyIdAndCharacterName()
	{
		var packet = CreateLegionInvitePacket("Lurion");

		Assert.Equal(0x01, packet.ExOpcode);
		Assert.Equal("Lurion", packet.CharacterName);
	}

	[Fact]
	public void ReadFrom_BrigadeGeneralTransferConsumesJavaEmptyIdAndCharacterName()
	{
		var packet = CreateBrigadeGeneralTransferPacket("Lurion");

		Assert.Equal(0x05, packet.ExOpcode);
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
	public void ReadFrom_LevelUpConsumesJavaEmptyFields()
	{
		var packet = CreateLevelUpPacket();

		Assert.Equal(0x0E, packet.ExOpcode);
	}

	[Fact]
	public void ReadFrom_LegionDominionSelectionConsumesJavaLocationId()
	{
		var packet = CreateLegionDominionSelectionPacket(5);

		Assert.Equal(0x10, packet.ExOpcode);
		Assert.Equal(5, packet.LegionDominionId);
	}

	[Fact]
	public void ReadFrom_LegionDominionRankingConsumesJavaStonespearId()
	{
		var packet = CreateLegionDominionRankingPacket(5);

		Assert.Equal(5, packet.StonespearId);
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
		var rejectedInvite = SmSystemMessage.MsgRejectedInviteGuild("Lurion");
		Assert.Equal(1390118, rejectedInvite.MessageId);
		Assert.Equal(["Lurion"], rejectedInvite.Parameters);
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
		Assert.Equal(1300315, SmSystemMessage.GuildChangeLevelDontHaveRight().MessageId);
		Assert.Equal(1300316, SmSystemMessage.GuildChangeLevelCantLevelUp().MessageId);
		Assert.Equal(1300317, SmSystemMessage.GuildChangeLevelNotEnoughPoint().MessageId);
		Assert.Equal(1300318, SmSystemMessage.GuildChangeLevelNotEnoughMember().MessageId);
		Assert.Equal(1300319, SmSystemMessage.GuildChangeLevelNotEnoughMoney().MessageId);
		var challengeTask = SmSystemMessage.GuildLevelUpChallengeTask(5);
		Assert.Equal(904452, challengeTask.MessageId);
		Assert.Equal(["5"], challengeTask.Parameters);
		var eventLevelUp = SmSystemMessage.GuildEventLevelUp(2);
		Assert.Equal(900700, eventLevelUp.MessageId);
		Assert.Equal(["2"], eventLevelUp.Parameters);
		var applyDominion = SmSystemMessage.MsgGuildApplyDominion("5");
		Assert.Equal(1402902, applyDominion.MessageId);
		Assert.Equal(["5"], applyDominion.Parameters);
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
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var registry = new CapturingConnectionRegistry(bystander);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
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
		Assert.Equal(11, bystander.LegionDeputyPermission);
		Assert.Equal(12, bystander.LegionCenturionPermission);
		Assert.Equal(13, bystander.LegionLegionaryPermission);
		Assert.Equal(14, bystander.LegionVolunteerPermission);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EditPermissionsMutatesRuntimeStateAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateEditPermissionsPacket(21, 22, 23, 24));

		Assert.Equal(21, player.LegionDeputyPermission);
		Assert.Equal(22, player.LegionCenturionPermission);
		Assert.Equal(23, player.LegionLegionaryPermission);
		Assert.Equal(24, player.LegionVolunteerPermission);
		Assert.Equal(21, bystander.LegionDeputyPermission);
		Assert.Equal(22, bystander.LegionCenturionPermission);
		Assert.Equal(23, bystander.LegionLegionaryPermission);
		Assert.Equal(24, bystander.LegionVolunteerPermission);
		Assert.Equal(11, outsider.LegionDeputyPermission);
		Assert.Equal(12, outsider.LegionCenturionPermission);
		Assert.Equal(13, outsider.LegionLegionaryPermission);
		Assert.Equal(14, outsider.LegionVolunteerPermission);

		AssertLegionEditPermissionsPacket(Assert.Single(pair.SentPackets), 21, 22, 23, 24);
		var bystanderPacket = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionEditPermissionsPacket(bystanderPacket.Packet, 21, 22, 23, 24);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeSelfIntroInvalidValueReturnsWithoutMutationLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var registry = new CapturingConnectionRegistry(bystander);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateLegionPlayer();
		player.LegionSelfIntro = "Old intro";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeSelfIntroPacket(string.Empty));

		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.DirectPackets);
		Assert.Equal("Old intro", player.LegionSelfIntro);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeSelfIntroMutatesStateAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 88;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
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
		var bystanderDelivery = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionUpdateSelfIntroPacket(
			bystanderDelivery.Packet,
			player.ObjectId,
			"Ready for sieges");
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameMutatesActiveMemberAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionNickname = "Old";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("tester", "Siege Lead"));

		Assert.Equal("Siege Lead", player.LegionNickname);
		Assert.Equal(0, repository.LoadLegionMemberByNameCalls);
		Assert.Equal(0, repository.SaveLegionMemberNicknameCalls);
		AssertLegionUpdateNicknamePacket(Assert.Single(pair.SentPackets), player.ObjectId, "Siege Lead");
		var bystanderDelivery = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionUpdateNicknamePacket(bystanderDelivery.Packet, player.ObjectId, "Siege Lead");
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknamePersistsOfflineMemberAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(3003, "Watcher");
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 88;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
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
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("lurion", "Scout"));

		Assert.Equal(1, repository.LoadLegionMemberByNameCalls);
		Assert.Equal((77, "Lurion"), repository.LoadedLegionMemberByNameRequest);
		Assert.Equal(1, repository.SaveLegionMemberNicknameCalls);
		Assert.Equal((2002, "Scout"), repository.SavedLegionMemberNickname);
		AssertLegionUpdateNicknamePacket(Assert.Single(pair.SentPackets), 2002, "Scout");
		var bystanderDelivery = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionUpdateNicknamePacket(bystanderDelivery.Packet, 2002, "Scout");
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeNicknameOnlineTargetMutatesAndBroadcastsWithoutPersistenceLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		target.LegionNickname = "Old";
		var bystander = CreateLegionPlayer(3003, "Watcher");
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 88;
		var registry = new CapturingConnectionRegistry(target, bystander, outsider);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("lurion", "Scout"));

		Assert.Equal("Scout", target.LegionNickname);
		Assert.Equal(0, repository.LoadLegionMemberByNameCalls);
		Assert.Equal(0, repository.SaveLegionMemberNicknameCalls);
		AssertLegionUpdateNicknamePacket(Assert.Single(pair.SentPackets), target.ObjectId, "Scout");
		Assert.Equal([target.ObjectId, bystander.ObjectId], registry.DirectPackets.Select(delivery => delivery.PlayerObjectId));
		foreach (var delivery in registry.DirectPackets)
			AssertLegionUpdateNicknamePacket(delivery.Packet, target.ObjectId, "Scout");
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
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
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var registry = new CapturingConnectionRegistry(bystander);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionNickname = "Old";
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeNicknamePacket("Tester", string.Empty));

		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.DirectPackets);
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
		var bystander = CreateLegionPlayer(3003, "Watcher");
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 88;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
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
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
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
		var bystanderDelivery = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionUpdateMemberPacket(
			bystanderDelivery.Packet,
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
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeRankOnlineMemberMutatesStateAndBroadcastsWithoutPersistenceLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var target = new Player
		{
			ObjectId = 2003,
			Name = "Serin",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionRank = LegionRanks.Volunteer,
			PlayerClass = "RANGER",
			Position = new WorldPosition(220010000, 0, 0, 0, 0),
		};
		var bystander = CreateLegionPlayer(3003, "Watcher");
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 88;
		var registry = new CapturingConnectionRegistry(target, bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeRankPacket(rankId: 1, "serin"));

		Assert.Equal(LegionRanks.Deputy, target.LegionRank);
		Assert.Equal(0, repository.LoadLegionMemberByNameCalls);
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
		Assert.Equal([target.ObjectId, bystander.ObjectId], registry.DirectPackets.Select(delivery => delivery.PlayerObjectId));
		foreach (var delivery in registry.DirectPackets)
		{
			AssertLegionUpdateMemberPacket(
				delivery.Packet,
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
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LevelUpRejectsNonBrigadeGeneralLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Deputy;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLevelUpPacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300315, response.MessageId);
		Assert.Equal(0, repository.CountLegionMembersCalls);
		Assert.Equal(0, repository.SaveLegionLevelUpMutationCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LevelUpRejectsInsufficientKinahBeforeMemberCheckLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, options: CreateLevelUpOptions(requiredKinah: 100));
		var player = CreateBrigadeGeneralPlayer();
		player.LegionLevel = 1;
		player.InventoryItems = [CreateKinah(99, player.ObjectId)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLevelUpPacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300319, response.MessageId);
		Assert.Equal(0, repository.CountLegionMembersCalls);
		Assert.Equal(0, repository.SaveLegionLevelUpMutationCalls);
		Assert.Equal(99, player.InventoryItems.Single().Count);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LevelUpRejectsMissingChallengeTaskForLevelFivePlusLikeJavaDefault()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			options: CreateLevelUpOptions(),
			challengeTaskTable: CreateChallengeTaskTable());
		var player = CreateBrigadeGeneralPlayer();
		player.LegionLevel = 5;
		player.InventoryItems = [CreateKinah(1000, player.ObjectId)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLevelUpPacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(904452, response.MessageId);
		Assert.Equal(["5"], response.Parameters);
		Assert.Equal(1, repository.LoadLegionChallengeTasksCalls);
		Assert.Equal(77, repository.LoadedLegionChallengeTasksLegionId);
		Assert.Equal(0, repository.CountLegionMembersCalls);
		Assert.Equal(0, repository.SaveLegionLevelUpMutationCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LevelUpWithCompletedChallengeTasksMutatesLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			CountLegionMembersResult = 1,
			LoadedLegionChallengeTasks = CreateCompletedLevelFiveChallengeRows(),
		};
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			options: CreateLevelUpOptions(),
			challengeTaskTable: CreateChallengeTaskTable());
		var player = CreateBrigadeGeneralPlayer();
		player.LegionLevel = 5;
		player.InventoryItems = [CreateKinah(1500, player.ObjectId)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLevelUpPacket());

		Assert.Equal(1, repository.LoadLegionChallengeTasksCalls);
		Assert.Equal(77, repository.LoadedLegionChallengeTasksLegionId);
		Assert.Equal(1, repository.CountLegionMembersCalls);
		Assert.Equal(77, repository.CountedLegionMembersLegionId);
		Assert.Equal(1, repository.SaveLegionLevelUpMutationCalls);
		Assert.Equal(6, repository.SavedLegionLevelUpMutation?.LegionLevel);
		Assert.Equal(500, repository.SavedLegionLevelUpMutation?.KinahItemUpdate?.Count);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.LevelUp, "Tester", "6"), repository.InsertedLegionHistory);
		Assert.Equal(6, player.LegionLevel);
		Assert.Collection(
			pair.SentPackets,
			packet => AssertLegionEditLevelPacket(packet, 6),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(900700, message.MessageId);
				Assert.Equal(["6"], message.Parameters);
			});
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LevelUpMutatesKinahLevelHistoryAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		bystander.LegionLevel = 1;
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		outsider.LegionLevel = 1;
		var repository = new EmptyPlayerEnterWorldRepository { CountLegionMembersResult = 2 };
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			registry,
			options: CreateLevelUpOptions(requiredKinah: 100, requiredMembers: 2, requiredContribution: 50));
		var player = CreateBrigadeGeneralPlayer();
		player.LegionLevel = 1;
		player.LegionContributionPoints = 50;
		player.InventoryItems = [CreateKinah(500, player.ObjectId)];
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLevelUpPacket());

		Assert.Equal(1, repository.CountLegionMembersCalls);
		Assert.Equal(77, repository.CountedLegionMembersLegionId);
		Assert.Equal(1, repository.SaveLegionLevelUpMutationCalls);
		Assert.Equal(2, repository.SavedLegionLevelUpMutation?.LegionLevel);
		Assert.Equal(400, repository.SavedLegionLevelUpMutation?.KinahItemUpdate?.Count);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.LevelUp, "Tester", "2"), repository.InsertedLegionHistory);
		Assert.Equal(2, player.LegionLevel);
		Assert.Equal(2, bystander.LegionLevel);
		Assert.Equal(1, outsider.LegionLevel);
		Assert.Collection(
			pair.SentPackets,
			packet => AssertLegionEditLevelPacket(packet, 2),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(900700, message.MessageId);
				Assert.Equal(["2"], message.Parameters);
			});
		Assert.Equal(2, registry.DirectPackets.Count(delivery => delivery.PlayerObjectId == bystander.ObjectId));
		AssertLegionEditLevelPacket(registry.DirectPackets[0].Packet, 2);
		var directMessage = Assert.IsType<SmSystemMessage>(registry.DirectPackets[1].Packet);
		Assert.Equal(900700, directMessage.MessageId);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionJoinRejectsNonDeputyOrBrigadeGeneralLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Centurion;
		player.LegionCurrentLegionDominion = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionSelectionPacket(5));

		Assert.Equal(0, repository.TryAddLegionDominionParticipantCalls);
		Assert.Equal(0, repository.SaveLegionCurrentDominionCalls);
		Assert.Equal(0, player.LegionCurrentLegionDominion);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionJoinRejectsAlreadySelectedLegionLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionCurrentLegionDominion = 7;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionSelectionPacket(5));

		Assert.Equal(0, repository.TryAddLegionDominionParticipantCalls);
		Assert.Equal(0, repository.SaveLegionCurrentDominionCalls);
		Assert.Equal(7, player.LegionCurrentLegionDominion);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionJoinDuplicateParticipantDoesNotMutateLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository { TryAddLegionDominionParticipantResult = false };
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionCurrentLegionDominion = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionSelectionPacket(5));

		Assert.Equal(1, repository.TryAddLegionDominionParticipantCalls);
		Assert.Equal((5, 77), repository.AddedLegionDominionParticipant);
		Assert.Equal(0, repository.SaveLegionCurrentDominionCalls);
		Assert.Equal(0, player.LegionCurrentLegionDominion);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionJoinPersistsStateAndBroadcastsInfoLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		bystander.LegionCurrentLegionDominion = 0;
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		outsider.LegionCurrentLegionDominion = 0;
		var repository = new EmptyPlayerEnterWorldRepository();
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		var runtimeContext = await CreateRuntimeContextWithLegionDominionDataAsync();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry, runtimeContext);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionCurrentLegionDominion = 0;
		SetActivePlayer(pair.Connection, player);
		var expectedDominionName = ChatUtil.L10n(404634);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionSelectionPacket(5));

		Assert.Equal(1, repository.TryAddLegionDominionParticipantCalls);
		Assert.Equal((5, 77), repository.AddedLegionDominionParticipant);
		Assert.Equal(1, repository.SaveLegionCurrentDominionCalls);
		Assert.Equal((77, 5), repository.SavedLegionCurrentDominion);
		Assert.Equal(5, player.LegionCurrentLegionDominion);
		Assert.Equal(5, bystander.LegionCurrentLegionDominion);
		Assert.Equal(0, outsider.LegionCurrentLegionDominion);
		Assert.Collection(
			pair.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402902, message.MessageId);
				Assert.Equal([expectedDominionName], message.Parameters);
			},
			packet => AssertLegionInfoPacket(packet, currentLegionDominion: 5));
		Assert.Equal(2, registry.DirectPackets.Count(delivery => delivery.PlayerObjectId == bystander.ObjectId));
		var directMessage = Assert.IsType<SmSystemMessage>(registry.DirectPackets[0].Packet);
		Assert.Equal(1402902, directMessage.MessageId);
		Assert.Equal([expectedDominionName], directMessage.Parameters);
		AssertLegionInfoPacket(registry.DirectPackets[1].Packet, currentLegionDominion: 5);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionJoinFallsBackToIdWhenStaticDataMissing()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionCurrentLegionDominion = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionSelectionPacket(5));

		var message = Assert.IsType<SmSystemMessage>(pair.SentPackets[0]);
		Assert.Equal(1402902, message.MessageId);
		Assert.Equal(["5"], message.Parameters);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(7)]
	public async Task HandleInfrastructurePacketAsync_DominionRankingRejectsInvalidIdsLikeJava(int stonespearId)
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionDominionParticipants =
			[
				new LegionDominionParticipantRow(77, "Hydrated Legion", 100, 20, 2000),
			],
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionRankingPacket(stonespearId));

		Assert.Equal(0, repository.LoadLegionDominionParticipantsCalls);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_DominionRankingLoadsRowsAndSendsRankLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionDominionParticipants =
			[
				new LegionDominionParticipantRow(10, "North", 80, 30, 3000),
				new LegionDominionParticipantRow(77, "Hydrated Legion", 90, 40, 2000),
				new LegionDominionParticipantRow(20, "South", 90, 50, 1000),
			],
		};
		await using var pair = await TestConnectionPair.CreateAsync(repository);
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionDominionRankingPacket(5));

		Assert.Equal(1, repository.LoadLegionDominionParticipantsCalls);
		Assert.Equal(5, repository.LoadedLegionDominionParticipantsRequest);
		var packet = Assert.IsType<SmLegionDominionRank>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(2, reader.ReadC());
		Assert.Equal(3, reader.ReadH());
		AssertLegionDominionRankRow(reader, 90, 50, 1000, "South");
		AssertLegionDominionRankRow(reader, 90, 40, 2000, "Hydrated Legion");
		AssertLegionDominionRankRow(reader, 80, 30, 3000, "North");
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
	public async Task HandleInfrastructurePacketAsync_BrigadeGeneralTransferMissingOnlineTargetSendsNoSuchUserLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: new CapturingConnectionRegistry());
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("missing"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300270, response.MessageId);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteMissingOnlineTargetSendsNoUserLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: new CapturingConnectionRegistry());
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("missing"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300253, response.MessageId);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteWithoutPermissionSendsNoRightLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Legionary;
		player.LegionLegionaryPermission = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300252, response.MessageId);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteSameLegionTargetSendsAlreadyMemberLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300255, response.MessageId);
		Assert.Equal(["Lurion"], response.Parameters);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteOtherLegionTargetSendsOtherMemberLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		target.LegionId = 88;
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300256, response.MessageId);
		Assert.Equal(["Lurion"], response.Parameters);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteGuildDeniedTargetSendsRejectLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		target.Settings.Deny = PlayerSettings.DenyGuildRequests;
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390118, response.MessageId);
		Assert.Equal(["Lurion"], response.Parameters);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteOtherRaceTargetSendsRaceDenialLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion", "ASMODIANS");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300311, response.MessageId);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteOtherRaceAllowedByConfigSendsQuestionLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion", "ASMODIANS");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(
			connectionRegistry: registry,
			options: new GameServerOptions
			{
				Legion = new GameServerLegionOptions { InviteOtherFactionEnabled = true },
			});
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		Assert.True(target.ResponseRequester.ContainsRequest(SmQuestionWindow.GuildInviteDoYouAcceptInvitation));
		var pending = Assert.IsType<PendingLegionInviteRequest>(target.PendingLegionInviteRequest);
		Assert.Equal(player.ObjectId, pending.InviterObjectId);
		Assert.Equal(target.ObjectId, pending.TargetObjectId);
		var notification = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300258, notification.MessageId);
		Assert.Equal(["Lurion"], notification.Parameters);
		var directQuestion = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == target.ObjectId);
		var question = Assert.IsType<SmQuestionWindow>(directQuestion.Packet);
		Assert.Equal(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, question.Code);
		AssertQuestionWindowPayload(
			question,
			SmQuestionWindow.GuildInviteDoYouAcceptInvitation,
			"Hydrated Legion",
			"4",
			"Tester",
			senderObjectId: 0);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteBusyTargetSendsBusyLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		Assert.True(target.ResponseRequester.PutRequest(
			SmQuestionWindow.GuildInviteDoYouAcceptInvitation,
			new QuestionResponseRequest(9999, QuestionResponseRequestKind.Unknown)));
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300325, response.MessageId);
		Assert.True(target.ResponseRequester.ContainsRequest(SmQuestionWindow.GuildInviteDoYouAcceptInvitation));
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionInviteOnlineTargetStoresRequestAndSendsQuestionLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));

		Assert.True(target.ResponseRequester.ContainsRequest(SmQuestionWindow.GuildInviteDoYouAcceptInvitation));
		var pending = Assert.IsType<PendingLegionInviteRequest>(target.PendingLegionInviteRequest);
		Assert.Equal(player.ObjectId, pending.InviterObjectId);
		Assert.Equal(target.ObjectId, pending.TargetObjectId);
		Assert.Equal(player.LegionId, pending.LegionId);
		var notification = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300258, notification.MessageId);
		Assert.Equal(["Lurion"], notification.Parameters);
		var directQuestion = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == target.ObjectId);
		var question = Assert.IsType<SmQuestionWindow>(directQuestion.Packet);
		Assert.Equal(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, question.Code);
		AssertQuestionWindowPayload(
			question,
			SmQuestionWindow.GuildInviteDoYouAcceptInvitation,
			"Hydrated Legion",
			"4",
			"Tester",
			senderObjectId: 0);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteDenyConsumesRequestAndNotifiesInviterLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var player = CreateBrigadeGeneralPlayer();
		var registry = new CapturingConnectionRegistry(player, target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));
		pair.SentPackets.Clear();
		registry.DirectPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 0));

		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Empty(pair.SentPackets);
		var notification = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == player.ObjectId);
		var message = Assert.IsType<SmSystemMessage>(notification.Packet);
		Assert.Equal(1300259, message.MessageId);
		Assert.Equal(["Lurion"], message.Parameters);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteDenyWithoutPendingRequestIsSideEffectFreeLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		target.PendingLegionInviteRequest = new PendingLegionInviteRequest(
			1001,
			"Tester",
			target.ObjectId,
			target.Name,
			77,
			"Hydrated Legion",
			4);
		var registry = new CapturingConnectionRegistry(CreateBrigadeGeneralPlayer(), target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 0));

		Assert.NotNull(target.PendingLegionInviteRequest);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public void SmLegionMemberList_WritesJavaLastChunkPayload()
	{
		var packet = new SmLegionMemberList(
			[
				new LegionMemberListEntry(
					PlayerObjectId: 1001,
					Name: "Tester",
					PlayerClass: "RANGER",
					Level: 47,
					Rank: LegionRanks.Centurion,
					WorldId: 210010000,
					IsOnline: true,
					SelfIntro: "Ready",
					Nickname: "Scout"),
			],
			isFirst: true,
			isLast: true,
			gameServerId: 1);

		AssertLegionMemberListPacket(
			packet,
			isFirst: true,
			signedCount: -1,
			[
				new ExpectedLegionMemberListRow(
					PlayerObjectId: 1001,
					Name: "Tester",
					ClassId: 5,
					Level: 47,
					RankId: LegionRanks.GetRankId(LegionRanks.Centurion),
					WorldId: 210010000,
					Online: true,
					SelfIntro: "Ready",
					Nickname: "Scout",
					LastOnlineEpochSeconds: 0,
					HouseAddressId: 0,
					HouseDoorStateId: 0,
					GameServerId: 1),
			]);
	}

	[Fact]
	public void SmLegionAddMember_WritesJavaInviteAcceptanceShape()
	{
		var player = CreateUnguildedPlayer(2002, "Lurion");
		player.LegionRank = LegionRanks.Volunteer;
		player.Level = 14;
		player.Position = new WorldPosition(210010000, 0, 0, 0, 0);

		AssertLegionAddMemberPacket(
			new SmLegionAddMember(player, isMember: false, gameServerId: 1, messageId: 1300260, text: player.Name),
			playerObjectId: 2002,
			name: "Lurion",
			rankId: LegionRanks.GetRankId(LegionRanks.Volunteer),
			isMember: false,
			classId: 5,
			level: 14,
			worldId: 210010000,
			gameServerId: 1,
			messageId: 1300260,
			text: "Lurion");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteAcceptPersistsMemberMutatesStateAndBroadcastsLikeJava()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		target.Level = 14;
		var player = CreateBrigadeGeneralPlayer();
		player.LegionAnnouncement = "Welcome aboard";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;
		player.LegionEmblemId = 3;
		player.LegionEmblemType = 1;
		player.LegionEmblemColorA = 255;
		player.LegionEmblemColorR = 10;
		player.LegionEmblemColorG = 20;
		player.LegionEmblemColorB = 30;
		var bystander = new Player
		{
			ObjectId = 3003,
			Name = "Watcher",
			Race = "ELYOS",
			PlayerClass = "CLERIC",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionLevel = 4,
			LegionRank = LegionRanks.Legionary,
			LegionNickname = "Healer",
			LegionSelfIntro = "Standing by",
			Position = new WorldPosition(220010000, 0, 0, 0, 0),
		};
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 99;
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionMembers =
			[
				new LegionMemberSnapshot(
					player.ObjectId,
					77,
					player.Name,
					LegionRanks.BrigadeGeneral,
					string.Empty,
					string.Empty,
					true,
					player.PlayerClass,
					player.Exp,
					player.Position.WorldId,
					player.LastOnline),
				new LegionMemberSnapshot(
					target.ObjectId,
					77,
					target.Name,
					LegionRanks.Volunteer,
					string.Empty,
					string.Empty,
					true,
					target.PlayerClass,
					target.Exp,
					target.Position.WorldId,
					target.LastOnline),
				new LegionMemberSnapshot(
					bystander.ObjectId,
					77,
					bystander.Name,
					LegionRanks.Legionary,
					"Stale",
					"Old intro",
					false,
					"RANGER",
					0,
					110010000,
					DateTimeOffset.FromUnixTimeSeconds(2_000).UtcDateTime),
				new LegionMemberSnapshot(
					5005,
					77,
					"Offline",
					LegionRanks.Centurion,
					"Crafter",
					"Sleeping",
					false,
					"SORCERER",
					0,
					120010000,
					DateTimeOffset.FromUnixTimeSeconds(3_000).UtcDateTime),
			],
		};
		var registry = new CapturingConnectionRegistry(player, target, bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));
		pair.SentPackets.Clear();
		registry.DirectPackets.Clear();
		registry.VisibleBroadcasts.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 1));

		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Equal(1, repository.LoadLegionMembersCalls);
		Assert.Equal(77, repository.LoadedLegionMembersLegionId);
		Assert.Equal(1, repository.SaveNewLegionMemberCalls);
		Assert.Equal((77, target.ObjectId, LegionRanks.Volunteer), repository.SavedNewLegionMember);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.Join, "Lurion", string.Empty), repository.InsertedLegionHistory);
		Assert.Equal(77, target.LegionId);
		Assert.Equal("Hydrated Legion", target.LegionName);
		Assert.Equal(4, target.LegionLevel);
		Assert.Equal(LegionRanks.Volunteer, target.LegionRank);
		Assert.Equal("Welcome aboard", target.LegionAnnouncement);
		Assert.Equal(1_771_234_500, target.LegionAnnouncementEpochSeconds);
		Assert.Equal(3, target.LegionEmblemId);
		Assert.Empty(pair.SentPackets);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);

		var info = Assert.Single(
			registry.DirectPackets,
			delivery => delivery.PlayerObjectId == target.ObjectId && delivery.Packet is SmLegionInfo);
		AssertLegionInfoAnnouncementPacket(info.Packet, "Welcome aboard", 1_771_234_500);
		var notice = Assert.Single(
			registry.DirectPackets,
				delivery => delivery.PlayerObjectId == target.ObjectId
				&& delivery.Packet is SmSystemMessage message
				&& message.MessageId == 1400019);
		var noticePacket = Assert.IsType<SmSystemMessage>(notice.Packet);
		Assert.Equal(["Welcome aboard", "1771234500", "2"], noticePacket.Parameters);

		var memberList = Assert.Single(
			registry.DirectPackets,
			delivery => delivery.PlayerObjectId == target.ObjectId && delivery.Packet is SmLegionMemberList);
		AssertLegionMemberListPacket(
			memberList.Packet,
			isFirst: true,
			signedCount: -3,
			[
				new ExpectedLegionMemberListRow(
					PlayerObjectId: player.ObjectId,
					Name: "Tester",
					ClassId: 0,
					Level: 1,
					RankId: LegionRanks.GetRankId(LegionRanks.BrigadeGeneral),
					WorldId: 0,
					Online: true,
					SelfIntro: string.Empty,
					Nickname: string.Empty,
					LastOnlineEpochSeconds: 0,
					HouseAddressId: 0,
					HouseDoorStateId: 0,
					GameServerId: 1),
				new ExpectedLegionMemberListRow(
					PlayerObjectId: bystander.ObjectId,
					Name: "Watcher",
					ClassId: 10,
					Level: 1,
					RankId: LegionRanks.GetRankId(LegionRanks.Legionary),
					WorldId: 220010000,
					Online: true,
					SelfIntro: "Standing by",
					Nickname: "Healer",
					LastOnlineEpochSeconds: 0,
					HouseAddressId: 0,
					HouseDoorStateId: 0,
					GameServerId: 1),
				new ExpectedLegionMemberListRow(
					PlayerObjectId: 5005,
					Name: "Offline",
					ClassId: 7,
					Level: 1,
					RankId: LegionRanks.GetRankId(LegionRanks.Centurion),
					WorldId: 120010000,
					Online: false,
					SelfIntro: "Sleeping",
					Nickname: "Crafter",
					LastOnlineEpochSeconds: 3000,
					HouseAddressId: 0,
					HouseDoorStateId: 0,
					GameServerId: 1),
			]);

		var memberAdds = registry.DirectPackets
			.Where(delivery => delivery.Packet is SmLegionAddMember)
			.ToArray();
		Assert.Equal([player.ObjectId, target.ObjectId, bystander.ObjectId], memberAdds.Select(delivery => delivery.PlayerObjectId));
		foreach (var delivery in memberAdds)
		{
			AssertLegionAddMemberPacket(
				delivery.Packet,
				playerObjectId: target.ObjectId,
				name: "Lurion",
				rankId: LegionRanks.GetRankId(LegionRanks.Volunteer),
				isMember: false,
				classId: 5,
				level: 14,
				worldId: 0,
				gameServerId: 1,
				messageId: 1300260,
				text: "Lurion");
		}
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.Packet is SmLegionUpdateMember);

		var refreshPackets = registry.DirectPackets
			.Where(delivery => delivery.Packet is SmLegionEdit)
			.ToArray();
		Assert.Equal([player.ObjectId, target.ObjectId, bystander.ObjectId], refreshPackets.Select(delivery => delivery.PlayerObjectId));
		foreach (var delivery in refreshPackets)
			AssertLegionEditPacket(delivery.Packet, expectedType: 0x08);

		var emblemBroadcast = Assert.Single(registry.VisibleBroadcasts, broadcast => broadcast.Packet is SmLegionUpdateEmblem);
		Assert.Equal(target.ObjectId, emblemBroadcast.SourceObjectId);
		Assert.True(emblemBroadcast.IncludeSourcePlayer);
		AssertLegionUpdateEmblemPacket(
			emblemBroadcast.Packet,
			legionId: 77,
			emblemId: 3,
			emblemType: 1,
			colorA: 255,
			colorR: 10,
			colorG: 20,
			colorB: 30);

		var titleBroadcast = Assert.Single(registry.VisibleBroadcasts, broadcast => broadcast.Packet is SmLegionUpdateTitle);
		Assert.Equal(target.ObjectId, titleBroadcast.SourceObjectId);
		Assert.True(titleBroadcast.IncludeSourcePlayer);
		AssertLegionUpdateTitlePacket(
			titleBroadcast.Packet,
			target.ObjectId,
			77,
			"Hydrated Legion",
			LegionRanks.GetRankId(LegionRanks.Volunteer));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteAcceptFailedInsertDoesNotMutateLikeJavaPersistenceGuard()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveNewLegionMemberResult = false };
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var player = CreateBrigadeGeneralPlayer();
		var registry = new CapturingConnectionRegistry(player, target);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));
		pair.SentPackets.Clear();
		registry.DirectPackets.Clear();
		registry.VisibleBroadcasts.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 1));

		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Equal(1, repository.SaveNewLegionMemberCalls);
		Assert.Equal((77, target.ObjectId, LegionRanks.Volunteer), repository.SavedNewLegionMember);
		Assert.Equal(0, repository.InsertLegionHistoryCalls);
		Assert.Equal(0, target.LegionId);
		Assert.Equal(string.Empty, target.LegionName);
		Assert.Equal(string.Empty, target.LegionRank);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.DirectPackets);
		Assert.Empty(registry.VisibleBroadcasts);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteAcceptChunksMemberListAtJavaSize()
	{
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var player = CreateBrigadeGeneralPlayer();
		var persistedRoster = CreateLargeLegionRoster(player, target);
		var repository = new EmptyPlayerEnterWorldRepository { LoadedLegionMembers = persistedRoster };
		var registry = new CapturingConnectionRegistry(player, target);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));
		pair.SentPackets.Clear();
		registry.DirectPackets.Clear();
		registry.VisibleBroadcasts.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 1));

		Assert.Equal(1, repository.LoadLegionMembersCalls);
		var memberLists = registry.DirectPackets
			.Where(delivery => delivery.PlayerObjectId == target.ObjectId && delivery.Packet is SmLegionMemberList)
			.Select(delivery => delivery.Packet)
			.ToArray();
		Assert.Equal(2, memberLists.Length);
		AssertLegionMemberListPacket(
			memberLists[0],
			isFirst: true,
			signedCount: 80,
			CreateExpectedLargeRosterRows(startOfflineIndex: 0, count: 79, includeLeader: true));
		AssertLegionMemberListPacket(
			memberLists[1],
			isFirst: false,
			signedCount: -2,
			CreateExpectedLargeRosterRows(startOfflineIndex: 79, count: 2, includeLeader: false));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LegionInviteAcceptFullLegionNotifiesInviterAndDoesNotPersistLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository { CountLegionMembersResult = 2 };
		var target = CreateUnguildedPlayer(2002, "Lurion");
		var player = CreateBrigadeGeneralPlayer();
		var registry = new CapturingConnectionRegistry(player, target);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			registry,
			options: CreateLegionInviteLimitOptions(level4MaxMembers: 2));
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateLegionInvitePacket("lurion"));
		pair.SentPackets.Clear();
		registry.DirectPackets.Clear();
		registry.VisibleBroadcasts.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildInviteDoYouAcceptInvitation, response: 1));

		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionInviteRequest);
		Assert.Equal(1, repository.CountLegionMembersCalls);
		Assert.Equal(77, repository.CountedLegionMembersLegionId);
		Assert.Equal(0, repository.SaveNewLegionMemberCalls);
		Assert.Equal(0, repository.InsertLegionHistoryCalls);
		Assert.Equal(0, target.LegionId);
		Assert.Equal(string.Empty, target.LegionName);
		Assert.Equal(string.Empty, target.LegionRank);
		Assert.Empty(pair.SentPackets);
		var notification = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == player.ObjectId);
		var message = Assert.IsType<SmSystemMessage>(notification.Packet);
		Assert.Equal(1300257, message.MessageId);
		Assert.Empty(message.Parameters);
		Assert.Empty(registry.VisibleBroadcasts);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_BrigadeGeneralTransferStoresRequesterConfirmAndSendsQuestionLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));

		Assert.True(player.ResponseRequester.ContainsRequest(904979));
		Assert.NotNull(player.PendingLegionBrigadeGeneralTransferRequest);
		var question = Assert.IsType<SmQuestionWindow>(Assert.Single(pair.SentPackets));
		Assert.Equal(904979, question.Code);
		AssertQuestionWindowPayload(question, 904979, "Lurion", string.Empty, string.Empty, senderObjectId: 0);
		Assert.Empty(registry.DirectPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_BrigadeGeneralTransferConfirmSendsOfferToTargetLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));
		pair.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(904979, response: 1));

		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Null(player.PendingLegionBrigadeGeneralTransferRequest);
		Assert.True(target.ResponseRequester.ContainsRequest(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer));
		Assert.NotNull(target.PendingLegionBrigadeGeneralTransferRequest);
		var sentOffer = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300330, sentOffer.MessageId);
		Assert.Equal(["Lurion"], sentOffer.Parameters);
		var directQuestion = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == target.ObjectId);
		var question = Assert.IsType<SmQuestionWindow>(directQuestion.Packet);
		Assert.Equal(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer, question.Code);
		AssertQuestionWindowPayload(
			question,
			SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer,
			"Tester",
			string.Empty,
			string.Empty,
			senderObjectId: player.ObjectId);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_BrigadeGeneralTransferConfirmRejectsBusyTargetLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		Assert.True(target.ResponseRequester.PutRequest(
			SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer,
			new QuestionResponseRequest(9999, QuestionResponseRequestKind.Unknown)));
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));
		pair.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(904979, response: 1));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300331, response.MessageId);
		Assert.Empty(registry.DirectPackets);
		Assert.True(target.ResponseRequester.ContainsRequest(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_BrigadeGeneralTransferOfferDenyNotifiesRequesterLikeJava()
	{
		var target = CreateLegionPlayer(2002, "Lurion");
		var registry = new CapturingConnectionRegistry(target);
		await using var pair = await TestConnectionPair.CreateAsync(connectionRegistry: registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));
		await pair.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(904979, response: 1));
		registry.DirectPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(target, CreateQuestionResponse(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer, response: 0));

		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingLegionBrigadeGeneralTransferRequest);
		var notification = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == player.ObjectId);
		var message = Assert.IsType<SmSystemMessage>(notification.Packet);
		Assert.Equal(1300332, message.MessageId);
		Assert.Equal(["Lurion"], message.Parameters);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_BrigadeGeneralTransferOfferAcceptMutatesRanksPersistsHistoryAndBroadcastsLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var target = CreateLegionPlayer(2002, "Lurion");
		target.LegionRank = LegionRanks.Legionary;
		var bystander = CreateLegionPlayer(3003, "Watcher");
		bystander.LegionRank = LegionRanks.Centurion;
		var outsider = CreateLegionPlayer(4004, "Outsider");
		outsider.LegionId = 88;
		outsider.LegionRank = LegionRanks.Legionary;
		var player = CreateBrigadeGeneralPlayer();
		var registry = new CapturingConnectionRegistry(player, target, bystander, outsider);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));
		await pair.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(904979, response: 1));
		registry.DirectPackets.Clear();
		pair.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer, response: 1));

		Assert.Equal(LegionRanks.Centurion, player.LegionRank);
		Assert.Equal(LegionRanks.BrigadeGeneral, target.LegionRank);
		Assert.Equal(2, repository.SaveLegionMemberRankCalls);
		Assert.Equal(
			[(player.ObjectId, LegionRanks.Centurion), (target.ObjectId, LegionRanks.BrigadeGeneral)],
			repository.SavedLegionMemberRanks);
		Assert.Equal(1, repository.InsertLegionHistoryCalls);
		Assert.Equal((77, LegionHistoryActions.Appointed, "Lurion", string.Empty), repository.InsertedLegionHistory);
		Assert.Empty(pair.SentPackets);
		Assert.Equal(
			[player.ObjectId, target.ObjectId, bystander.ObjectId, player.ObjectId, target.ObjectId, bystander.ObjectId, player.ObjectId, target.ObjectId, bystander.ObjectId],
			registry.DirectPackets.Select(delivery => delivery.PlayerObjectId));
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
		foreach (var delivery in registry.DirectPackets.Take(3))
		{
			AssertLegionUpdateMemberPacket(
				delivery.Packet,
				playerObjectId: player.ObjectId,
				rankId: LegionRanks.GetRankId(LegionRanks.Centurion),
				classId: 0,
				level: 1,
				worldId: 0,
				online: true,
				lastOnlineEpochSeconds: 0,
				gameServerId: 1,
				messageId: 0,
				text: string.Empty);
		}

		foreach (var delivery in registry.DirectPackets.Skip(3).Take(3))
		{
			AssertLegionUpdateMemberPacket(
				delivery.Packet,
				playerObjectId: target.ObjectId,
				rankId: LegionRanks.GetRankId(LegionRanks.BrigadeGeneral),
				classId: 0,
				level: 1,
				worldId: 0,
				online: true,
				lastOnlineEpochSeconds: 0,
				gameServerId: 1,
				messageId: 1300273,
				text: "Lurion");
		}

		foreach (var delivery in registry.DirectPackets.Skip(6).Take(3))
			AssertLegionEditPacket(delivery.Packet, expectedType: 0x08);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_BrigadeGeneralTransferOfferAcceptWithStaleRequesterDoesNotMutateLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var target = CreateLegionPlayer(2002, "Lurion");
		target.LegionRank = LegionRanks.Legionary;
		var player = CreateBrigadeGeneralPlayer();
		var registry = new CapturingConnectionRegistry(player, target);
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		SetActivePlayer(pair.Connection, player);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateBrigadeGeneralTransferPacket("lurion"));
		await pair.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(904979, response: 1));
		player.LegionRank = LegionRanks.Centurion;
		registry.DirectPackets.Clear();
		pair.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(
			target,
			CreateQuestionResponse(SmQuestionWindow.GuildChangeMasterDoYouAcceptOffer, response: 1));

		Assert.Equal(LegionRanks.Centurion, player.LegionRank);
		Assert.Equal(LegionRanks.Legionary, target.LegionRank);
		Assert.Equal(0, repository.SaveLegionMemberRankCalls);
		Assert.Equal(0, repository.InsertLegionHistoryCalls);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.DirectPackets);
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
		var titleBroadcast = Assert.Single(registry.VisibleBroadcasts);
		Assert.Equal(target.ObjectId, titleBroadcast.SourceObjectId);
		Assert.True(titleBroadcast.IncludeSourcePlayer);
		AssertLegionUpdateTitlePacket(titleBroadcast.Packet, target.ObjectId, 0, string.Empty, LegionRanks.GetRankId(LegionRanks.Legionary));
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
		var titleBroadcast = Assert.Single(registry.VisibleBroadcasts);
		Assert.Equal(player.ObjectId, titleBroadcast.SourceObjectId);
		Assert.True(titleBroadcast.IncludeSourcePlayer);
		AssertLegionUpdateTitlePacket(titleBroadcast.Packet, player.ObjectId, 0, string.Empty, LegionRanks.GetRankId(LegionRanks.Legionary));
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
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var registry = new CapturingConnectionRegistry(bystander);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateLegionPlayer();
		player.LegionRank = LegionRanks.Volunteer;
		player.LegionVolunteerPermission = 0;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket("New notice"));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1300276, response.MessageId);
		Assert.Equal(string.Empty, player.LegionAnnouncement);
		Assert.Equal(string.Empty, bystander.LegionAnnouncement);
		Assert.Empty(registry.DirectPackets);
		Assert.Equal(0, repository.SaveLegionAnnouncementCalls);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChangeAnnouncementPersistsStateAndBroadcastsLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket("New notice"));

		Assert.Equal("New notice", player.LegionAnnouncement);
		Assert.True(player.LegionAnnouncementEpochSeconds > 0);
		Assert.Equal("New notice", bystander.LegionAnnouncement);
		Assert.Equal(player.LegionAnnouncementEpochSeconds, bystander.LegionAnnouncementEpochSeconds);
		Assert.Equal(string.Empty, outsider.LegionAnnouncement);
		Assert.Equal(0, outsider.LegionAnnouncementEpochSeconds);
		Assert.Equal(1, repository.SaveLegionAnnouncementCalls);
		Assert.NotNull(repository.SavedLegionAnnouncement);
		Assert.Equal(player.LegionId, repository.SavedLegionAnnouncement.Value.LegionId);
		Assert.Equal("New notice", repository.SavedLegionAnnouncement.Value.Announcement);
		Assert.NotNull(repository.SavedLegionAnnouncement.Value.AnnouncementTime);
		Assert.Collection(
			pair.SentPackets,
			packet =>
			{
				var response = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300277, response.MessageId);
			},
			packet => AssertLegionEditAnnouncementPacket(packet, "New notice", player.LegionAnnouncementEpochSeconds));
		var bystanderPacket = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionEditAnnouncementPacket(bystanderPacket.Packet, "New notice", player.LegionAnnouncementEpochSeconds);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
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
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertLegionEditAnnouncementPacket(packet, new string('A', 256), player.LegionAnnouncementEpochSeconds));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ClearAnnouncementPersistsNullAndBroadcastsInfoLikeJava()
	{
		var bystander = CreateLegionPlayer(2002, "Watcher");
		bystander.LegionAnnouncement = "Old notice";
		bystander.LegionAnnouncementEpochSeconds = 1_771_234_500;
		var outsider = CreateLegionPlayer(3003, "Outsider");
		outsider.LegionId = 99;
		outsider.LegionAnnouncement = "Other notice";
		outsider.LegionAnnouncementEpochSeconds = 1_771_234_501;
		var registry = new CapturingConnectionRegistry(bystander, outsider);
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, registry);
		var player = CreateBrigadeGeneralPlayer();
		player.LegionAnnouncement = "Old notice";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChangeAnnouncementPacket(string.Empty));

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390128, response.MessageId);
		Assert.Equal(string.Empty, player.LegionAnnouncement);
		Assert.Equal(0, player.LegionAnnouncementEpochSeconds);
		Assert.Equal(string.Empty, bystander.LegionAnnouncement);
		Assert.Equal(0, bystander.LegionAnnouncementEpochSeconds);
		Assert.Equal("Other notice", outsider.LegionAnnouncement);
		Assert.Equal(1_771_234_501, outsider.LegionAnnouncementEpochSeconds);
		Assert.Equal(1, repository.SaveLegionAnnouncementCalls);
		Assert.NotNull(repository.SavedLegionAnnouncement);
		Assert.Equal(player.LegionId, repository.SavedLegionAnnouncement.Value.LegionId);
		Assert.Null(repository.SavedLegionAnnouncement.Value.Announcement);
		Assert.Null(repository.SavedLegionAnnouncement.Value.AnnouncementTime);
		var bystanderPacket = Assert.Single(registry.DirectPackets, delivery => delivery.PlayerObjectId == bystander.ObjectId);
		AssertLegionInfoAnnouncementPacket(bystanderPacket.Packet, string.Empty, 0);
		Assert.DoesNotContain(registry.DirectPackets, delivery => delivery.PlayerObjectId == outsider.ObjectId);
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

	private static CmLegion CreateLegionInvitePacket(string memberName)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x01);
		buffer.WriteD(0);
		buffer.WriteS(memberName);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateBrigadeGeneralTransferPacket(string memberName)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x05);
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

	private static CmLegion CreateLevelUpPacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0E);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateLegionDominionSelectionPacket(int legionDominionId)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x10);
		buffer.WriteD(legionDominionId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegionDominionRequestRanking CreateLegionDominionRankingPacket(int stonespearId)
	{
		var packet = new CmLegionDominionRequestRanking(29, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(stonespearId);
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
			Race = "ELYOS",
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

	private static Player CreateUnguildedPlayer(int objectId, string name, string race = "ELYOS")
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = "RANGER",
		};
	}

	private static InventoryItem CreateKinah(long count, int ownerId)
	{
		return new InventoryItem
		{
			ObjectId = 9001,
			ItemId = 182400001,
			Count = count,
			OwnerId = ownerId,
			Location = 0,
		};
	}

	private static GameServerOptions CreateLevelUpOptions(
		int requiredKinah = 0,
		int requiredMembers = 1,
		int requiredContribution = 0,
		bool challengeTaskRequirementEnabled = true)
	{
		return new GameServerOptions
		{
			Legion = new GameServerLegionOptions
			{
				LevelRequiredKinah = [requiredKinah, 1000, 1000, 1000, 1000, 1000, 1000],
				LevelRequiredMembers = [requiredMembers, 1, 1, 1, 1, 1, 1],
				LevelRequiredContribution = [requiredContribution, 0, 0, 0, 0, 0, 0],
				ChallengeTaskRequirementEnabled = challengeTaskRequirementEnabled,
			},
		};
	}

	private static GameServerOptions CreateLegionInviteLimitOptions(int level4MaxMembers)
	{
		return new GameServerOptions
		{
			Legion = new GameServerLegionOptions
			{
				LevelMaxMembers = [30, 60, 90, level4MaxMembers, 150, 180, 210, 240],
			},
		};
	}

	private static ChallengeTaskTable CreateChallengeTaskTable()
	{
		return new ChallengeTaskTable(
			[
				new ChallengeTaskSummary(
					300,
					"LEGION",
					"ELYOS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(17000, 6, 5),
						new ChallengeQuestSummary(17001, 12, 6),
						new ChallengeQuestSummary(17002, 42, 7),
					]),
				new ChallengeTaskSummary(
					400,
					"LEGION",
					"ASMODIANS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(27000, 6, 5),
						new ChallengeQuestSummary(27001, 12, 6),
						new ChallengeQuestSummary(27002, 42, 7),
					]),
			]);
	}

	private static ChallengeTaskProgressRow[] CreateCompletedLevelFiveChallengeRows()
	{
		return
		[
			new ChallengeTaskProgressRow(300, 17000, 6),
			new ChallengeTaskProgressRow(300, 17001, 12),
			new ChallengeTaskProgressRow(300, 17002, 42),
		];
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

	private static IReadOnlyList<LegionMemberSnapshot> CreateLargeLegionRoster(Player player, Player acceptedPlayer)
	{
		var members = new List<LegionMemberSnapshot>
		{
			new(
				player.ObjectId,
				player.LegionId,
				player.Name,
				LegionRanks.BrigadeGeneral,
				string.Empty,
				string.Empty,
				true,
				player.PlayerClass,
				player.Exp,
				player.Position.WorldId,
				player.LastOnline),
			new(
				acceptedPlayer.ObjectId,
				player.LegionId,
				acceptedPlayer.Name,
				LegionRanks.Volunteer,
				string.Empty,
				string.Empty,
				true,
				acceptedPlayer.PlayerClass,
				acceptedPlayer.Exp,
				acceptedPlayer.Position.WorldId,
				acceptedPlayer.LastOnline),
		};

		for (var index = 0; index < 81; index++)
			members.Add(CreateOfflineLargeRosterMember(index));

		return members;
	}

	private static LegionMemberSnapshot CreateOfflineLargeRosterMember(int index)
	{
		return new LegionMemberSnapshot(
			5000 + index,
			77,
			$"Offline{index}",
			LegionRanks.Legionary,
			$"N{index}",
			$"S{index}",
			false,
			"RANGER",
			0,
			120010000 + index,
			DateTimeOffset.FromUnixTimeSeconds(10_000 + index).UtcDateTime);
	}

	private static IReadOnlyList<ExpectedLegionMemberListRow> CreateExpectedLargeRosterRows(
		int startOfflineIndex,
		int count,
		bool includeLeader)
	{
		var rows = new List<ExpectedLegionMemberListRow>();
		if (includeLeader)
		{
			rows.Add(new ExpectedLegionMemberListRow(
				PlayerObjectId: 1001,
				Name: "Tester",
				ClassId: 0,
				Level: 1,
				RankId: LegionRanks.GetRankId(LegionRanks.BrigadeGeneral),
				WorldId: 0,
				Online: true,
				SelfIntro: string.Empty,
				Nickname: string.Empty,
				LastOnlineEpochSeconds: 0,
				HouseAddressId: 0,
				HouseDoorStateId: 0,
				GameServerId: 1));
		}

		for (var index = startOfflineIndex; index < startOfflineIndex + count; index++)
		{
			rows.Add(new ExpectedLegionMemberListRow(
				PlayerObjectId: 5000 + index,
				Name: $"Offline{index}",
				ClassId: 5,
				Level: 1,
				RankId: LegionRanks.GetRankId(LegionRanks.Legionary),
				WorldId: 120010000 + index,
				Online: false,
				SelfIntro: $"S{index}",
				Nickname: $"N{index}",
				LastOnlineEpochSeconds: 10_000 + index,
				HouseAddressId: 0,
				HouseDoorStateId: 0,
				GameServerId: 1));
		}

		return rows;
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithLegionDominionDataAsync()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-cm-legion-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var cacheFile = Path.Combine(tempPath, "static_data.xml");
			File.WriteAllText(
				cacheFile,
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<legion_dominion_template>
						<legion_dominion_location id="5" world_id="210070000" zone="LegionDominionArea_05" race="ELYOS" name_id="404634" />
					</legion_dominion_template>
				</static_data>
				""");
			var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);
			var dataManagerConstructor = typeof(DataManager).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				[typeof(StaticData)],
				null);
			Assert.NotNull(dataManagerConstructor);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager((DataManager)dataManagerConstructor.Invoke([staticData]));
			return runtimeContext;
		}
		finally
		{
			Directory.Delete(tempPath, recursive: true);
		}
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

	private static void AssertLegionUpdateSelfIntroPacket(GameServerPacket packet, int playerObjectId, string selfIntro)
	{
		var response = Assert.IsType<SmLegionUpdateSelfIntro>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(selfIntro, reader.ReadS());
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

	private static void AssertLegionAddMemberPacket(
		GameServerPacket packet,
		int playerObjectId,
		string name,
		int rankId,
		bool isMember,
		int classId,
		int level,
		int worldId,
		int gameServerId,
		int messageId,
		string text)
	{
		var response = Assert.IsType<SmLegionAddMember>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(name, reader.ReadS());
		Assert.Equal(rankId, reader.ReadC());
		Assert.Equal(isMember ? 1 : 0, reader.ReadC());
		Assert.Equal(classId, reader.ReadC());
		Assert.Equal(level, reader.ReadC());
		Assert.Equal(worldId, reader.ReadD());
		Assert.Equal(gameServerId, reader.ReadD());
		Assert.Equal(messageId, reader.ReadD());
		Assert.Equal(text, reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionMemberListPacket(
		GameServerPacket packet,
		bool isFirst,
		short signedCount,
		IReadOnlyList<ExpectedLegionMemberListRow> rows)
	{
		var response = Assert.IsType<SmLegionMemberList>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(isFirst ? 1 : 0, reader.ReadC());
		Assert.Equal(signedCount, reader.ReadSignedH());
		foreach (var row in rows)
			AssertLegionMemberListRow(reader, row);
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionMemberListRow(PacketBuffer reader, ExpectedLegionMemberListRow row)
	{
		Assert.Equal(row.PlayerObjectId, reader.ReadD());
		Assert.Equal(row.Name, reader.ReadS());
		Assert.Equal(row.ClassId, reader.ReadC());
		Assert.Equal(row.Level, reader.ReadD());
		Assert.Equal(row.RankId, reader.ReadC());
		Assert.Equal(row.WorldId, reader.ReadD());
		Assert.Equal(row.Online ? 1 : 0, reader.ReadC());
		Assert.Equal(row.SelfIntro, reader.ReadS());
		Assert.Equal(row.Nickname, reader.ReadS());
		Assert.Equal(row.LastOnlineEpochSeconds, reader.ReadD());
		Assert.Equal(row.HouseAddressId, reader.ReadD());
		Assert.Equal(row.HouseDoorStateId, reader.ReadD());
		Assert.Equal(row.GameServerId, reader.ReadD());
	}

	private static void AssertLegionUpdateEmblemPacket(
		GameServerPacket packet,
		int legionId,
		int emblemId,
		int emblemType,
		int colorA,
		int colorR,
		int colorG,
		int colorB)
	{
		var response = Assert.IsType<SmLegionUpdateEmblem>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(legionId, reader.ReadD());
		Assert.Equal(emblemId, reader.ReadC());
		Assert.Equal(emblemType, reader.ReadC());
		Assert.Equal(colorA, reader.ReadC());
		Assert.Equal(colorR, reader.ReadC());
		Assert.Equal(colorG, reader.ReadC());
		Assert.Equal(colorB, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionEditPacket(GameServerPacket packet, int expectedType)
	{
		var response = Assert.IsType<SmLegionEdit>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(expectedType, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
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

	private static void AssertLegionUpdateTitlePacket(
		GameServerPacket packet,
		int playerObjectId,
		int legionId,
		string legionName,
		int rankId)
	{
		var response = Assert.IsType<SmLegionUpdateTitle>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(legionId, reader.ReadD());
		Assert.Equal(legionName, reader.ReadS());
		Assert.Equal(rankId, reader.ReadC());
	}

	private static void AssertLegionEditLevelPacket(GameServerPacket packet, int legionLevel)
	{
		var response = Assert.IsType<SmLegionEdit>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(legionLevel, reader.ReadC());
	}

	private static void AssertLegionEditPermissionsPacket(
		GameServerPacket packet,
		int deputyPermission,
		int centurionPermission,
		int legionaryPermission,
		int volunteerPermission)
	{
		var response = Assert.IsType<SmLegionEdit>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(0x02, reader.ReadC());
		Assert.Equal(deputyPermission, reader.ReadSignedH());
		Assert.Equal(centurionPermission, reader.ReadSignedH());
		Assert.Equal(legionaryPermission, reader.ReadSignedH());
		Assert.Equal(volunteerPermission, reader.ReadSignedH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionEditAnnouncementPacket(GameServerPacket packet, string announcement, int announcementEpochSeconds)
	{
		var response = Assert.IsType<SmLegionEdit>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(0x05, reader.ReadC());
		Assert.Equal(announcement, reader.ReadS());
		Assert.Equal(announcementEpochSeconds, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionInfoPacket(GameServerPacket packet, int currentLegionDominion)
	{
		var response = Assert.IsType<SmLegionInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal("Hydrated Legion", reader.ReadS());
		reader.ReadC();
		reader.ReadD();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadQ();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(currentLegionDominion, reader.ReadD());
	}

	private static void AssertLegionInfoAnnouncementPacket(GameServerPacket packet, string announcement, int announcementEpochSeconds)
	{
		var response = Assert.IsType<SmLegionInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal("Hydrated Legion", reader.ReadS());
		reader.ReadC();
		reader.ReadD();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadSignedH();
		reader.ReadQ();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(announcement, reader.ReadS());
		if (!string.IsNullOrEmpty(announcement))
		{
			Assert.Equal(announcementEpochSeconds, reader.ReadD());
			Assert.Equal(string.Empty, reader.ReadS());
		}
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLegionDominionRankRow(
		PacketBuffer reader,
		int points,
		int survivedTime,
		long epochSeconds,
		string legionName)
	{
		Assert.Equal(points, reader.ReadD());
		Assert.Equal(survivedTime, reader.ReadD());
		Assert.Equal(epochSeconds, reader.ReadQ());
		Assert.Equal(legionName, reader.ReadS());
	}

	private static void AssertQuestionWindowPayload(
		SmQuestionWindow packet,
		int questionId,
		string parameter0,
		string parameter1,
		string parameter2,
		int senderObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(questionId, reader.ReadD());
		Assert.Equal(parameter0, reader.ReadS());
		Assert.Equal(parameter1, reader.ReadS());
		Assert.Equal(parameter2, reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(senderObjectId, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
	}

	private static CmQuestionResponse CreateQuestionResponse(int questionId, byte response)
	{
		var packet = new CmQuestionResponse(50, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(questionId);
		buffer.WriteC(response);
		buffer.WriteC(0);
		buffer.WriteH(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
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
			GameServerRuntimeContext? runtimeContext = null,
			GameServerOptions? options = null,
			ChallengeTaskTable? challengeTaskTable = null)
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
					options: options,
					runtimeContext: runtimeContext,
					playerEnterWorldRepository: playerEnterWorldRepository,
					connectionRegistry: connectionRegistry,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt,
					challengeTaskTable: challengeTaskTable);
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

	private sealed record ExpectedLegionMemberListRow(
		int PlayerObjectId,
		string Name,
		int ClassId,
		int Level,
		int RankId,
		int WorldId,
		bool Online,
		string SelfIntro,
		string Nickname,
		int LastOnlineEpochSeconds,
		int HouseAddressId,
		int HouseDoorStateId,
		int GameServerId);

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyList<Player> _players;

		public CapturingConnectionRegistry(params Player[] players)
		{
			_players = players;
		}

		public List<(int PlayerObjectId, GameServerPacket Packet)> DirectPackets { get; } = [];

		public List<(WorldPosition SourcePosition, int SourceObjectId, GameServerPacket Packet, bool IncludeSourcePlayer)> VisibleBroadcasts { get; } = [];

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
			VisibleBroadcasts.Add((sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(1);
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
