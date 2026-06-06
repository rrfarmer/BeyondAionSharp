using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using GameWorld = Aion.GameServer.World.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmAtreianPassportTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaAtreianPassportOpcodeAsInGameOnly()
	{
		Assert.IsType<CmAtreianPassport>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(248, buffer => buffer.WriteH(0)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(248, buffer => buffer.WriteH(0)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_SentinelCountConsumesCompletePassportPairsUntilTrailingBytes()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xffff);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(1001);
		buffer.WriteD(1717286400);
		buffer.WriteD(9999);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(-1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000, 1717286400]));
	}

	[Fact]
	public void ReadFrom_PositiveCountConsumesOnlyDeclaredPassportPairs()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(2002);
		buffer.WriteD(1717286400);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000]));
	}

	[Fact]
	public void SmAtreianPassport_WritePayload_WritesJavaSnapshotFields()
	{
		var passport = new PlayerPassport(
			PassportId: 1001,
			Rewarded: false,
			ArriveDate: DateTimeOffset.FromUnixTimeSeconds(1_717_200_000).UtcDateTime);
		var payload = SerializeUnencryptedPayload(new SmAtreianPassport(
			[passport],
			stamps: 7,
			creationDate: new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc)));

		Assert.Equal(2020, ReadShort(payload, 0));
		Assert.Equal(5, ReadShort(payload, 2));
		Assert.Equal(6, ReadShort(payload, 4));
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(1001, ReadInt(payload, 8));
		Assert.Equal(7, ReadInt(payload, 12));
		Assert.Equal(1, ReadInt(payload, 16)); // Passport.RewardStatus.AVAILABLE.
		Assert.Equal(1_717_200_000, ReadInt(payload, 20));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_AtreianPassportSendsLiveSnapshotForActivePlayer()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = new Player
		{
			ObjectId = 5001,
			AccountId = 77,
			Name = "PassportTester",
			CreationDate = new DateTime(2021, 3, 4, 12, 30, 0, DateTimeKind.Utc),
			PassportStamps = 3,
			Passports =
			[
				new PlayerPassport(
					PassportId: 2002,
					Rewarded: true,
					ArriveDate: DateTimeOffset.FromUnixTimeSeconds(1_717_286_400).UtcDateTime)
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		SetActivePlayer(pair.Connection, player);
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var response = Assert.Single(pair.SentPackets);
		var passport = Assert.IsType<SmAtreianPassport>(response);
		var payload = SerializeUnencryptedPayload(passport);
		Assert.Equal(2021, ReadShort(payload, 0));
		Assert.Equal(3, ReadShort(payload, 2));
		Assert.Equal(4, ReadShort(payload, 4));
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(2002, ReadInt(payload, 8));
		Assert.Equal(3, ReadInt(payload, 12));
		Assert.Equal(2, ReadInt(payload, 16)); // Passport.RewardStatus.TAKEN.
		Assert.Equal(1_717_286_400, ReadInt(payload, 20));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_AtreianPassportClaimsMatchingRestoredPassport()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			new GameWorld(NullLogger<GameWorld>.Instance),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportActiveClock);
		var arriveDate = DateTimeOffset.FromUnixTimeSeconds(1_717_286_400).UtcDateTime;
		var player = new Player
		{
			ObjectId = 5002,
			AccountId = 78,
			Name = "PassportClaimer",
			Level = 50,
			CreationDate = new DateTime(2021, 3, 4, 12, 30, 0, DateTimeKind.Utc),
			PassportStamps = 5,
			Passports =
			[
				new PlayerPassport(
					PassportId: 9,
					Rewarded: false,
					ArriveDate: arriveDate)
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		SetActivePlayer(pair.Connection, player);
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(9);
		buffer.WriteD(1_717_286_400);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		Assert.Equal(1, repository.SaveInventoryRewardMutationCalls);
		Assert.NotNull(repository.SavedInventoryRewardMutation);
		var savedReward = repository.SavedInventoryRewardMutation.Value;
		Assert.Equal(5002, savedReward.PlayerObjectId);
		Assert.Empty(savedReward.UpdatedRewardItems);
		var addedReward = Assert.Single(savedReward.AddedRewardItems);
		Assert.Equal(166000010, addedReward.ItemId);
		Assert.Equal(1, addedReward.Count);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == addedReward.ObjectId && item.ItemId == 166000010);

		Assert.Equal(1, repository.UpdateAccountPassportRewardedCalls);
		Assert.NotNull(repository.UpdatedAccountPassportRewarded);
		var update = repository.UpdatedAccountPassportRewarded.Value;
		Assert.Equal(78, update.AccountId);
		Assert.Equal(9, update.Passport.PassportId);
		Assert.True(update.Passport.Rewarded);
		Assert.True(Assert.Single(player.Passports).Rewarded);

		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmAtreianPassport>(packet));
		var response = pair.SentPackets[1];
		var passport = Assert.IsType<SmAtreianPassport>(response);
		var payload = SerializeUnencryptedPayload(passport);
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(9, ReadInt(payload, 8));
		Assert.Equal(5, ReadInt(payload, 12));
		Assert.Equal(2, ReadInt(payload, 16)); // Passport.RewardStatus.TAKEN.
		Assert.Equal(1_717_286_400, ReadInt(payload, 20));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_AtreianPassportDeletesExpiredRewardClaim()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			new GameWorld(NullLogger<GameWorld>.Instance),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportActiveClock);
		var arriveDate = DateTimeOffset.FromUnixTimeSeconds(1_400_000_000).UtcDateTime;
		var player = new Player
		{
			ObjectId = 5003,
			AccountId = 79,
			Name = "PassportExpired",
			Level = 50,
			CreationDate = new DateTime(2021, 3, 4, 12, 30, 0, DateTimeKind.Utc),
			PassportStamps = 6,
			Passports =
			[
				new PlayerPassport(
					PassportId: 1,
					Rewarded: false,
					ArriveDate: arriveDate)
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		SetActivePlayer(pair.Connection, player);
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(1);
		buffer.WriteD(1_400_000_000);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		Assert.Equal(1, repository.DeleteAccountPassportCalls);
		Assert.NotNull(repository.DeletedAccountPassport);
		var deleted = repository.DeletedAccountPassport.Value;
		Assert.Equal(79, deleted.AccountId);
		Assert.Equal(1, deleted.Passport.PassportId);
		Assert.Equal(arriveDate, deleted.Passport.ArriveDate);
		Assert.Equal(0, repository.SaveInventoryRewardMutationCalls);
		Assert.Equal(0, repository.UpdateAccountPassportRewardedCalls);
		Assert.Empty(player.InventoryItems);
		Assert.Empty(player.Passports);

		var response = Assert.Single(pair.SentPackets);
		var passport = Assert.IsType<SmAtreianPassport>(response);
		var payload = SerializeUnencryptedPayload(passport);
		Assert.Equal(0, ReadShort(payload, 6));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_AtreianPassportDisabledGateSuppressesSnapshotAndClaims()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			new GameWorld(NullLogger<GameWorld>.Instance),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			() => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
		var arriveDate = DateTimeOffset.FromUnixTimeSeconds(1_717_286_400).UtcDateTime;
		var player = new Player
		{
			ObjectId = 5004,
			AccountId = 80,
			Name = "PassportDisabled",
			Level = 50,
			CreationDate = new DateTime(2021, 3, 4, 12, 30, 0, DateTimeKind.Utc),
			PassportStamps = 7,
			Passports =
			[
				new PlayerPassport(
					PassportId: 9,
					Rewarded: false,
					ArriveDate: arriveDate)
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		SetActivePlayer(pair.Connection, player);
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(9);
		buffer.WriteD(1_717_286_400);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		Assert.Equal(0, repository.SaveInventoryRewardMutationCalls);
		Assert.Equal(0, repository.UpdateAccountPassportRewardedCalls);
		Assert.Equal(0, repository.DeleteAccountPassportCalls);
		Assert.Empty(player.InventoryItems);
		Assert.False(Assert.Single(player.Passports).Rewarded);
		Assert.Empty(pair.SentPackets);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EnterWorldSendsAtreianPassportLoginSnapshotAndRewardMessage()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var expectedDailyPassportIds = GetActiveDailyPassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			AtreianPassportLoginClock().UtcDateTime);
		var player = new Player
		{
			ObjectId = 5005,
			AccountId = 81,
			Name = "PassportLogin",
			Level = 50,
			CreationDate = new DateTime(2014, 1, 1, 12, 30, 0, DateTimeKind.Utc),
			LastOnline = DateTime.Now.AddMinutes(-5),
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedPlayer = player,
			MarkPlayerOnlineResult = true,
		};
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			CreateWorld(),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext,
			atreianPassportClock: AtreianPassportLoginClock);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportLoginClock);
		SetAccountId(pair.Connection, player.AccountId);
		var packet = new CmEnterWorld(8, new HashSet<GameConnectionState> { GameConnectionState.Authed });
		using var buffer = new PacketBuffer();
		buffer.WriteD(player.ObjectId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var message = Assert.Single(pair.SentPackets.OfType<SmSystemMessage>(), packet => packet.MessageId == 1402601);
		Assert.Empty(message.Parameters);
		var passportPacket = pair.SentPackets.OfType<SmAtreianPassport>().LastOrDefault();
		Assert.NotNull(passportPacket);
		Assert.Equal(1, repository.SaveAccountPassportLoginMutationCalls);
		Assert.Equal(1, player.PassportStamps);
		Assert.Equal(expectedDailyPassportIds, player.Passports.Select(passport => passport.PassportId).Order().ToArray());
		var payload = SerializeUnencryptedPayload(passportPacket);
		Assert.Equal(2014, ReadShort(payload, 0));
		Assert.Equal(1, ReadShort(payload, 2));
		Assert.Equal(1, ReadShort(payload, 4));
		Assert.Equal(expectedDailyPassportIds.Length, ReadShort(payload, 6));
		Assert.Equal(1, ReadInt(payload, 12));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EnterWorldSendsPassportLimitRemovalBeforeAttendanceReward()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var now = AtreianPassportCumulativeLoginClock();
		var activeDailyPassportIds = GetActiveDailyPassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			now.UtcDateTime);
		Assert.NotEmpty(activeDailyPassportIds);
		var activeDailyPassportId = activeDailyPassportIds[0];
		var existingPassports = Enumerable.Range(0, 45)
			.Select(index => new PlayerPassport(
				activeDailyPassportId,
				Rewarded: true,
				new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index)))
			.ToArray();
		var oldest = existingPassports[0];
		var rewardItemId = runtimeContext.DataManager.StaticData.AtreianPassports.GetAtreianPassportId(activeDailyPassportId)!.RewardItemId;
		var expectedRemovedItemName = runtimeContext.DataManager.StaticData.ItemTemplates.GetItemTemplate(rewardItemId)!.GetClientName();
		var player = new Player
		{
			ObjectId = 5008,
			AccountId = 84,
			Name = "PassportLimitLogin",
			Level = 50,
			CreationDate = now.UtcDateTime,
			LastOnline = DateTime.Now.AddMinutes(-5),
			Passports = existingPassports,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedPlayer = player,
			MarkPlayerOnlineResult = true,
		};
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			CreateWorld(),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext,
			atreianPassportClock: AtreianPassportCumulativeLoginClock);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportCumulativeLoginClock);
		SetAccountId(pair.Connection, player.AccountId);
		var packet = new CmEnterWorld(8, new HashSet<GameConnectionState> { GameConnectionState.Authed });
		using var buffer = new PacketBuffer();
		buffer.WriteD(player.ObjectId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var removeIndex = pair.SentPackets.FindIndex(packet => packet is SmSystemMessage { MessageId: 1402627 });
		var attendIndex = pair.SentPackets.FindIndex(packet => packet is SmSystemMessage { MessageId: 1402601 });
		Assert.True(removeIndex >= 0);
		Assert.True(attendIndex > removeIndex);
		var removeMessage = Assert.IsType<SmSystemMessage>(pair.SentPackets[removeIndex]);
		Assert.Equal([expectedRemovedItemName], removeMessage.Parameters);
		Assert.Equal(1, repository.DeleteAccountPassportCalls);
		Assert.NotNull(repository.DeletedAccountPassport);
		Assert.Equal((player.AccountId, oldest.PassportId, oldest.ArriveDate), (
			repository.DeletedAccountPassport.Value.AccountId,
			repository.DeletedAccountPassport.Value.Passport.PassportId,
			repository.DeletedAccountPassport.Value.Passport.ArriveDate));
		Assert.DoesNotContain(player.Passports, passport => passport.PassportId == oldest.PassportId && passport.ArriveDate == oldest.ArriveDate);

		var passportPacket = pair.SentPackets.OfType<SmAtreianPassport>().LastOrDefault();
		Assert.NotNull(passportPacket);
		var payload = SerializeUnencryptedPayload(passportPacket);
		Assert.Equal(player.Passports.Count, ReadShort(payload, 6));
		var removedArriveSeconds = (int)new DateTimeOffset(oldest.ArriveDate, TimeSpan.Zero).ToUnixTimeSeconds();
		for (var i = 0; i < ReadShort(payload, 6); i++)
		{
			var offset = 8 + (i * 16);
			Assert.False(ReadInt(payload, offset) == oldest.PassportId
				&& ReadInt(payload, offset + 12) == removedArriveSeconds);
		}
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EnterWorldIncludesCumulativePassportThresholdRowInLoginSnapshot()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var now = AtreianPassportCumulativeLoginClock();
		var startingStamps = 13;
		var expectedDailyPassportIds = GetActiveDailyPassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			now.UtcDateTime);
		var expectedCumulativePassportIds = GetActiveCumulativePassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			now.UtcDateTime);
		var expectedThresholdPassportIds = GetActiveCumulativePassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			now.UtcDateTime,
			startingStamps + 1);
		var expectedTakenFakePassportIds = expectedCumulativePassportIds
			.Where(id => runtimeContext.DataManager!.StaticData.AtreianPassports.GetAtreianPassportId(id)!.AttendNum <= startingStamps)
			.ToArray();
		var expectedUpcomingFakePassportIds = expectedCumulativePassportIds
			.Except(expectedThresholdPassportIds)
			.Except(expectedTakenFakePassportIds)
			.Order()
			.ToArray();
		var monthsAlive = 3;
		var expectedAnniversaryPassportIds = GetActiveAnniversaryPassportIds(
				runtimeContext.DataManager!.StaticData.AtreianPassports,
				now.UtcDateTime)
			.Where(id => runtimeContext.DataManager!.StaticData.AtreianPassports.GetAtreianPassportId(id)!.AttendNum <= monthsAlive)
			.Order()
			.ToArray();
		var expectedRealAnniversaryPassportIds = GetActiveAnniversaryPassportIds(
			runtimeContext.DataManager!.StaticData.AtreianPassports,
			now.UtcDateTime,
			monthsAlive);
		var expectedFakeAnniversaryPassportIds = expectedAnniversaryPassportIds
			.Except(expectedRealAnniversaryPassportIds)
			.Order()
			.ToArray();
		var expectedPassportIds = expectedDailyPassportIds
			.Concat(expectedCumulativePassportIds)
			.Concat(expectedAnniversaryPassportIds)
			.Order()
			.ToArray();
		var player = new Player
		{
			ObjectId = 5007,
			AccountId = 83,
			Name = "PassportCumulativeLogin",
			Level = 50,
			CreationDate = new DateTime(2014, 1, 1, 12, 30, 0, DateTimeKind.Utc),
			LastOnline = DateTime.Now.AddMinutes(-5),
			PassportStamps = startingStamps,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedPlayer = player,
			MarkPlayerOnlineResult = true,
		};
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			CreateWorld(),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext,
			atreianPassportClock: AtreianPassportCumulativeLoginClock);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportCumulativeLoginClock);
		SetAccountId(pair.Connection, player.AccountId);
		var packet = new CmEnterWorld(8, new HashSet<GameConnectionState> { GameConnectionState.Authed });
		using var buffer = new PacketBuffer();
		buffer.WriteD(player.ObjectId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var message = Assert.Single(pair.SentPackets.OfType<SmSystemMessage>(), packet => packet.MessageId == 1402601);
		Assert.Empty(message.Parameters);
		var passportPacket = pair.SentPackets.OfType<SmAtreianPassport>().LastOrDefault();
		Assert.NotNull(passportPacket);
		Assert.NotEmpty(expectedCumulativePassportIds);
		Assert.Equal(1, repository.SaveAccountPassportLoginMutationCalls);
		Assert.Equal(14, player.PassportStamps);
		Assert.NotEmpty(expectedTakenFakePassportIds);
		Assert.NotEmpty(expectedUpcomingFakePassportIds);
		Assert.NotEmpty(expectedRealAnniversaryPassportIds);
		Assert.NotEmpty(expectedFakeAnniversaryPassportIds);
		Assert.Equal(expectedPassportIds, player.Passports.Select(passport => passport.PassportId).Order().ToArray());
		var payload = SerializeUnencryptedPayload(passportPacket);
		var passportRows = ReadPassportRows(payload);
		Assert.Equal(2014, ReadShort(payload, 0));
		Assert.Equal(1, ReadShort(payload, 2));
		Assert.Equal(1, ReadShort(payload, 4));
		Assert.Equal(expectedPassportIds.Length, ReadShort(payload, 6));
		Assert.Equal(expectedPassportIds, passportRows.Keys.Order().ToArray());
		foreach (var passportId in expectedThresholdPassportIds)
			Assert.Equal((int)PlayerPassportRewardStatus.Available, passportRows[passportId]);
		foreach (var passportId in expectedTakenFakePassportIds)
			Assert.Equal((int)PlayerPassportRewardStatus.Taken, passportRows[passportId]);
		foreach (var passportId in expectedUpcomingFakePassportIds)
			Assert.Equal((int)PlayerPassportRewardStatus.Upcoming, passportRows[passportId]);
		foreach (var passportId in expectedRealAnniversaryPassportIds)
			Assert.Equal((int)PlayerPassportRewardStatus.Available, passportRows[passportId]);
		foreach (var passportId in expectedFakeAnniversaryPassportIds)
			Assert.Equal((int)PlayerPassportRewardStatus.Taken, passportRows[passportId]);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_EnterWorldOmitsExpiredAtreianPassportFromLoginSnapshot()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		var now = AtreianPassportLoginClock();
		var expiredArriveDate = now.AddDays(-2).UtcDateTime;
		var player = new Player
		{
			ObjectId = 5006,
			AccountId = 82,
			Name = "PassportLoginExpired",
			Level = 50,
			CreationDate = new DateTime(2014, 1, 1, 12, 30, 0, DateTimeKind.Utc),
			LastOnline = DateTime.Now.AddMinutes(-5),
			PassportStamps = 3,
			LastPassportStamp = now.UtcDateTime,
			Passports =
			[
				new PlayerPassport(1, Rewarded: false, expiredArriveDate),
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedPlayer = player,
			MarkPlayerOnlineResult = true,
		};
		var playerEnterWorldService = new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			CreateWorld(),
			NullLogger<PlayerEnterWorldService>.Instance,
			runtimeContext: runtimeContext,
			atreianPassportClock: AtreianPassportLoginClock);
		await using var pair = await TestConnectionPair.CreateAsync(
			repository,
			runtimeContext,
			playerEnterWorldService,
			new IDFactory(),
			AtreianPassportLoginClock);
		SetAccountId(pair.Connection, player.AccountId);
		var packet = new CmEnterWorld(8, new HashSet<GameConnectionState> { GameConnectionState.Authed });
		using var buffer = new PacketBuffer();
		buffer.WriteD(player.ObjectId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		Assert.DoesNotContain(pair.SentPackets.OfType<SmSystemMessage>(), packet => packet.MessageId == 1402601);
		var passportPacket = pair.SentPackets.OfType<SmAtreianPassport>().LastOrDefault();
		Assert.NotNull(passportPacket);
		Assert.Equal(1, repository.DeleteAccountPassportCalls);
		Assert.Empty(player.Passports);
		var payload = SerializeUnencryptedPayload(passportPacket);
		Assert.Equal(2014, ReadShort(payload, 0));
		Assert.Equal(1, ReadShort(payload, 2));
		Assert.Equal(1, ReadShort(payload, 4));
		Assert.Equal(0, ReadShort(payload, 6));
	}

	private static CmAtreianPassport CreatePacket()
	{
		return new CmAtreianPassport(248, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static DateTimeOffset AtreianPassportActiveClock()
	{
		return new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
	}

	private static DateTimeOffset AtreianPassportLoginClock()
	{
		return new DateTimeOffset(2014, 3, 20, 10, 15, 30, TimeSpan.Zero);
	}

	private static DateTimeOffset AtreianPassportCumulativeLoginClock()
	{
		return new DateTimeOffset(2014, 4, 2, 10, 15, 30, TimeSpan.Zero);
	}

	private static int[] GetActiveDailyPassportIds(AtreianPassportTable passports, DateTime now)
	{
		return passports.Passports
			.Where(passport => passport.Active
				&& passport.AttendType == "DAILY"
				&& passport.PeriodStart < now
				&& passport.PeriodEnd > now)
			.Select(passport => passport.Id)
			.Order()
			.ToArray();
	}

	private static int[] GetActiveCumulativePassportIds(AtreianPassportTable passports, DateTime now, int attendNum)
	{
		return passports.Passports
			.Where(passport => passport.Active
				&& passport.AttendType == "CUMULATIVE"
				&& passport.AttendNum == attendNum
				&& passport.PeriodStart < now
				&& passport.PeriodEnd > now)
			.Select(passport => passport.Id)
			.Order()
			.ToArray();
	}

	private static int[] GetActiveCumulativePassportIds(AtreianPassportTable passports, DateTime now)
	{
		return passports.Passports
			.Where(passport => passport.Active
				&& passport.AttendType == "CUMULATIVE"
				&& passport.PeriodStart < now
				&& passport.PeriodEnd > now)
			.Select(passport => passport.Id)
			.Order()
			.ToArray();
	}

	private static int[] GetActiveAnniversaryPassportIds(AtreianPassportTable passports, DateTime now, int attendNum)
	{
		return passports.Passports
			.Where(passport => passport.Active
				&& passport.AttendType == "ANNIVERSARY"
				&& passport.AttendNum == attendNum
				&& passport.PeriodStart < now
				&& passport.PeriodEnd > now)
			.Select(passport => passport.Id)
			.Order()
			.ToArray();
	}

	private static int[] GetActiveAnniversaryPassportIds(AtreianPassportTable passports, DateTime now)
	{
		return passports.Passports
			.Where(passport => passport.Active
				&& passport.AttendType == "ANNIVERSARY"
				&& passport.PeriodStart < now
				&& passport.PeriodEnd > now)
			.Select(passport => passport.Id)
			.Order()
			.ToArray();
	}

	private static Dictionary<int, int> ReadPassportRows(byte[] payload)
	{
		var count = ReadShort(payload, 6);
		var rows = new Dictionary<int, int>();
		for (var i = 0; i < count; i++)
		{
			var offset = 8 + (i * 16);
			rows.Add(ReadInt(payload, offset), ReadInt(payload, offset + 8));
		}

		return rows;
	}

	private static GameWorld CreateWorld()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.Initialize();
		return world;
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

	private static int ReadInt(byte[] payload, int offset)
	{
		return BitConverter.ToInt32(payload, offset);
	}

	private static int ReadShort(byte[] payload, int offset)
	{
		return BitConverter.ToUInt16(payload, offset);
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

	private static void SetAccountId(GameServerConnection connection, int accountId)
	{
		var accountIdField = typeof(GameServerConnection).GetField("_accountId", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(accountIdField);
		accountIdField.SetValue(connection, accountId);
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
			GameServerRuntimeContext? runtimeContext = null,
			PlayerEnterWorldService? playerEnterWorldService = null,
			IDFactory? idFactory = null,
			Func<DateTimeOffset>? atreianPassportClock = null)
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
					"atreian-passport-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					runtimeContext: runtimeContext,
					playerEnterWorldService: playerEnterWorldService,
					playerEnterWorldRepository: playerEnterWorldRepository,
					idFactory: idFactory,
					atreianPassportClock: atreianPassportClock,
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

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}
}
