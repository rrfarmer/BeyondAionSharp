using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class ExpirableTaskServiceTests
{
	[Fact]
	public async Task Tick_RemovesExpiredEmotionTitleAndMotion()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Kahrun",
			Position = new WorldPosition(210010000, 1, 2, 3, 32),
			TitleId = 5,
			BonusTitleId = 6,
			Emotions =
			[
				new PlayerEmotion(64, 999),
				new PlayerEmotion(65, 0),
			],
			Titles =
			[
				new PlayerTitle(5, 999),
				new PlayerTitle(6, 999),
				new PlayerTitle(7, 0),
			],
			Motions =
			[
				new PlayerMotion(11, 999, true),
				new PlayerMotion(12, 0, true),
			],
		};
		var sentPackets = new List<GameServerPacket>();
		var broadcastPackets = new List<GameServerPacket>();
		var titleTemplates = new TitleTemplateTable(
		[
			new TitleTemplateSummary(5, 412994, "display", "PC_ALL", Array.Empty<ItemStatModifier>()),
			new TitleTemplateSummary(6, 412995, "bonus", "PC_ALL", Array.Empty<ItemStatModifier>()),
			new TitleTemplateSummary(7, 412996, "permanent", "PC_ALL", Array.Empty<ItemStatModifier>()),
		]);

		service.RegisterPlayerExpirables(
			player,
			packet =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			packet =>
			{
				broadcastPackets.Add(packet);
				return Task.CompletedTask;
			},
			titleTemplates);
		await service.TickAsync();

		Assert.Equal([65], player.Emotions.Select(emotion => emotion.Id).ToArray());
		Assert.Equal([7], player.Titles.Select(title => title.Id).ToArray());
		Assert.Equal(-1, player.TitleId);
		Assert.Equal(-1, player.BonusTitleId);
		Assert.Equal([12], player.Motions.Select(motion => motion.Id).ToArray());
		Assert.Contains(sentPackets, packet => packet is SmEmotionList);
		Assert.Contains(sentPackets, packet => packet is SmTitleInfo);
		Assert.Contains(sentPackets, packet => packet is SmMotion);
		Assert.Contains(broadcastPackets, packet => packet is SmTitleInfo);
	}

	[Fact]
	public async Task Tick_KeepsExactExpiryAndUnregisteredPlayers()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var exactExpiryPlayer = new Player
		{
			ObjectId = 1001,
			Name = "Exact",
			Emotions = [new PlayerEmotion(64, 1000)],
		};
		var unregisteredPlayer = new Player
		{
			ObjectId = 1002,
			Name = "Gone",
			Emotions = [new PlayerEmotion(65, 999)],
		};

		service.RegisterPlayerExpirables(exactExpiryPlayer, _ => Task.CompletedTask);
		service.RegisterPlayerExpirables(unregisteredPlayer, _ => Task.CompletedTask);
		service.UnregisterPlayer(unregisteredPlayer);
		await service.TickAsync();

		Assert.Equal([64], exactExpiryPlayer.Emotions.Select(emotion => emotion.Id).ToArray());
		Assert.Equal([65], unregisteredPlayer.Emotions.Select(emotion => emotion.Id).ToArray());
	}
}
