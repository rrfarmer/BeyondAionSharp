using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PortalEntryValidationServiceTests
{
	[Fact]
	public void ValidateCooldown_AllowsWhenJavaCooldownCountIsBelowMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Single(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldown_RejectsWithJavaSystemMessageWhenCountMeetsMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateCooldown_RemovesExpiredJavaCooldownAndAllowsEntry()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 99_999, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_SkipsCooldownLockoutForSameSoloInstance()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId: 2);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.False(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_MarksReenterWhenRegisteredElsewhere()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.True(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_RejectsUnregisteredSoloEntryWhenCooldownLocked()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		Assert.Null(result.RegisteredInstance);
		Assert.False(result.Reenter);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_UsesJavaInstanceCooltimeLevelsWhenPortalPathMinIsMissing()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateEnterLevel_RejectsBelowJavaMinimumWithSystemMessage()
	{
		var player = new Player { Race = "ELYOS", Level = 24 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400179, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_RejectsAboveJavaMaximum()
	{
		var player = new Player { Race = "ASMODIANS", Level = 56 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400179, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_UsesPortalPathMinimumBeforeInstanceCooltimeMinimum()
	{
		var player = new Player { Race = "ELYOS", Level = 18 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			portalPathMinLevel: 18);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
	}

	[Fact]
	public void ValidateEnterLevel_ReturnsJavaErrLevelDialogWhenPortalPathProvidesOne()
	{
		var player = new Player { Race = "ELYOS", Level = 24 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			portalPathErrLevel: 1011,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(1011, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateEnterLevel_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player { Race = "ELYOS", Level = 1 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			bypassLevelRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateMentor_AllowsNonMentorWhenInstanceDisallowsMentors()
	{
		var player = new Player { IsMentor = false };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: false);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateMentor_RejectsMentorWhenJavaTemplateDisallowsMentors()
	{
		var player = new Player { IsMentor = true };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: false);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.MentorRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400766, packet.MessageId);
	}

	[Fact]
	public void ValidateMentor_AllowsMentorWhenJavaTemplateAllowsMentors()
	{
		var player = new Player { IsMentor = true };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: true);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_AllowsPcAllRaceLikeJavaPortalPathDefault()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(player, "PC_ALL");

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_AllowsMatchingPortalRace()
	{
		var player = new Player { Race = "ASMODIANS" };

		var result = PortalEntryValidationService.ValidateRace(player, "ASMODIANS");

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_ReturnsNoRightDialogForDialogNpcMismatch()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			npcIsDialogNpc: true,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateRace_ReturnsInvalidRaceSystemMessageForNonDialogNpcMismatch()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidateRace_RejectsWhenSuppliedSiegeOwnershipCheckFails()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"PC_ALL",
			siegeOwnerMatchesPlayerRace: false,
			npcIsDialogNpc: false);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidateRace_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			siegeOwnerMatchesPlayerRace: false,
			bypassRaceRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_AllowsWhenJavaAbyssRankMeetsPortalMinimum()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() with { Rank = 5 } };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 5, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_AllowsWhenJavaPortalMinimumIsZero()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 0, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_ReturnsNoRightDialogWhenRankIsBelowPortalMinimum()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() with { Rank = 4 } };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 5, npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RankRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private const int WorldId = 300030000;

	private static Player CreatePlayerWithCooldown(long reuseTimeMillis, int entryCount)
	{
		return new Player
		{
			ObjectId = 1001,
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
			{
				[WorldId] = new(WorldId, reuseTimeMillis, entryCount),
			},
		};
	}

	private static InstanceCooltimeTable CreateCooltimes(int maxCount)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, WorldId, "PC_ALL", maxCount),
		]);
	}

	private static InstanceCooltimeTable CreateCooltimesWithLevels(
		int elyosMin,
		int elyosMax,
		int asmodianMin,
		int asmodianMax)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				WorldId,
				"PC_ALL",
				MaxCount: 2,
				EnterMinLevelLight: elyosMin,
				EnterMaxLevelLight: elyosMax,
				EnterMinLevelDark: asmodianMin,
				EnterMaxLevelDark: asmodianMax),
		]);
	}

	private static InstanceCooltimeTable CreateCooltimesWithMentor(bool canEnterMentor)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				WorldId,
				"PC_ALL",
				MaxCount: 2,
				CanEnterMentor: canEnterMentor),
		]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMapsWithRegisteredSoloInstance(
		int playerObjectId,
		out WorldMapInstanceRuntimeState instance)
	{
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		instance = worldMaps.AddWorldMapInstance(WorldId, instanceId: 2, ownerId: 0, maxPlayers: 1)!;
		instance.Register(playerObjectId);
		return worldMaps;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
