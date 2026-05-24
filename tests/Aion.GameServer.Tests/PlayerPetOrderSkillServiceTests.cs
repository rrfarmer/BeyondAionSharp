using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public class PlayerPetOrderSkillServiceTests
{
	[Fact]
	public async Task ApplyUltraSkillOrder_MapsOrderSkillQueuesSummonOrderAndCreatesUseSkillPacket()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};

		var result = new PlayerPetOrderSkillService().ApplyUltraSkillOrder(
			player,
			new PlayerPetOrderSkillRequest(
				OrderSkillId: 3835,
				EffectedObjectId: 7001,
				EffectHate: 5,
				Release: true),
			dataManager.StaticData.PetSkills,
			dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerPetOrderSkillStatus.Applied, result.Status);
		Assert.Equal(22107, result.PetUseSkillId);
		Assert.Equal(1, result.PetUseSkillLevel);
		Assert.Equal(7001, result.TargetObjectId);
		Assert.Equal(5, result.Hate);
		Assert.True(result.Release);
		var order = Assert.Single(player.PetSkillOrders);
		Assert.Equal(22107, order.SkillId);
		Assert.Equal(1, order.SkillLevel);
		Assert.Equal(7001, order.TargetObjectId);
		Assert.Equal(5, order.Hate);
		Assert.True(order.Release);
		AssertSummonUseSkillPayload(Assert.IsType<SmSummonUseSkill>(result.Packet), 8001, 22107, 1, 7001);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 0)]
	public async Task ApplyUltraSkillOrder_UsesJavaHateThreshold(int effectHate, int expectedHate)
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};

		var result = new PlayerPetOrderSkillService().ApplyUltraSkillOrder(
			player,
			new PlayerPetOrderSkillRequest(3835, 7001, effectHate, Release: false),
			dataManager.StaticData.PetSkills,
			dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerPetOrderSkillStatus.Applied, result.Status);
		Assert.Equal(expectedHate, Assert.Single(player.PetSkillOrders).Hate);
	}

	[Fact]
	public async Task ApplyUltraSkillOrder_RequiresRepresentedSummonAndTemplateInputs()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerPetOrderSkillService();

		var missingSummon = service.ApplyUltraSkillOrder(
			new Player(),
			new PlayerPetOrderSkillRequest(3835, 7001, EffectHate: 3, Release: false),
			dataManager.StaticData.PetSkills,
			dataManager.StaticData.SkillTemplates);

		var missingMapping = service.ApplyUltraSkillOrder(
			new Player
			{
				HasPetSummon = true,
				PetSummonObjectId = 8001,
				PetSummonNpcId = 999999,
			},
			new PlayerPetOrderSkillRequest(3835, 7001, EffectHate: 3, Release: false),
			dataManager.StaticData.PetSkills,
			dataManager.StaticData.SkillTemplates);

		var missingTemplate = service.ApplyUltraSkillOrder(
			new Player
			{
				HasPetSummon = true,
				PetSummonObjectId = 8001,
				PetSummonNpcId = 833288,
			},
			new PlayerPetOrderSkillRequest(3835, 7001, EffectHate: 3, Release: false),
			dataManager.StaticData.PetSkills,
			new SkillTemplateTable([]));

		Assert.Equal(PlayerPetOrderSkillStatus.MissingSummon, missingSummon.Status);
		Assert.Equal(PlayerPetOrderSkillStatus.MissingPetSkillMapping, missingMapping.Status);
		Assert.Equal(PlayerPetOrderSkillStatus.MissingSkillTemplate, missingTemplate.Status);
		Assert.Equal(22107, missingTemplate.PetUseSkillId);
	}

	private static void AssertSummonUseSkillPayload(
		SmSummonUseSkill packet,
		int summonObjectId,
		int skillId,
		int skillLevel,
		int targetObjectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(summonObjectId, reader.ReadD());
		Assert.Equal(skillId, reader.ReadH());
		Assert.Equal(skillLevel, (int)reader.ReadC());
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "game-server")))
			directory = directory.Parent;

		return directory?.FullName ?? throw new InvalidOperationException("Unable to locate repository root.");
	}
}
