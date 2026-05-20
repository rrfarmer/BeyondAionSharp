using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class PlayerEnterWorldService
{
	private readonly GameServerOptions _options;
	private readonly IPlayerEnterWorldRepository _repository;
	private readonly GameWorld _world;
	private readonly ConcurrentDictionary<int, byte> _enteringWorld = new();
	private readonly ILogger<PlayerEnterWorldService> _logger;

	public PlayerEnterWorldService(
		GameServerOptions options,
		IPlayerEnterWorldRepository repository,
		GameWorld world,
		ILogger<PlayerEnterWorldService> logger)
	{
		_options = options;
		_repository = repository;
		_world = world;
		_logger = logger;
	}

	public async Task<PlayerEnterWorldResult> EnterWorldAsync(
		int accountId,
		int playerObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerEnterWorldService.enterWorld(AionConnection, int).
		if (accountId == 0)
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);

		var player = await _repository.LoadPlayerAsync(accountId, playerObjectId, cancellationToken);
		if (player == null)
		{
			_logger.LogWarning("Player enterWorld fail: character obj ID {PlayerObjectId} was not found on account ID {AccountId}", playerObjectId, accountId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}

		if (player.IsOnline)
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		if (IsInsideReentryWindow(player.LastOnline))
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		if (_world.TryGetObject(playerObjectId, out _))
		{
			_logger.LogWarning("Player enterWorld fail: duplicate character obj ID {PlayerObjectId} found in world", playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}

		if (!_enteringWorld.TryAdd(playerObjectId, 0))
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		try
		{
			player.InventoryItems = await _repository.LoadPlayerItemsAsync(playerObjectId, cancellationToken);
			player.Skills = await _repository.LoadPlayerSkillsAsync(playerObjectId, cancellationToken);
			player.SkillCooldowns = await _repository.LoadPlayerSkillCooldownsAsync(playerObjectId, cancellationToken);
			player.ItemCooldowns = await _repository.LoadPlayerItemCooldownsAsync(playerObjectId, cancellationToken);
			player.Quests = await _repository.LoadPlayerQuestsAsync(playerObjectId, cancellationToken);
			player.Motions = await _repository.LoadPlayerMotionsAsync(playerObjectId, cancellationToken);
			player.Settings = await _repository.LoadPlayerSettingsAsync(playerObjectId, cancellationToken);
			if (!_world.TryAddObject(playerObjectId, player))
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);

			var now = DateTime.Now;
			if (!await _repository.MarkPlayerOnlineAsync(playerObjectId, now, cancellationToken))
			{
				_world.TryRemoveObject(playerObjectId, out _);
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
			}

			player.IsOnline = true;
			player.LastOnline = now;
			_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) logged on", player.Name, playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.Ok, player);
		}
		catch (Exception ex)
		{
			_world.TryRemoveObject(playerObjectId, out _);
			_logger.LogError(ex, "Error during enter-world of player {PlayerObjectId}", playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}
		finally
		{
			_enteringWorld.TryRemove(playerObjectId, out _);
		}
	}

	private bool IsInsideReentryWindow(DateTime? lastOnline)
	{
		// Java parity: PlayerEnterWorldService lastOnline vs GSConfig.CHARACTER_REENTRY_TIME check.
		return lastOnline.HasValue
			&& DateTime.Now - lastOnline.Value < TimeSpan.FromSeconds(_options.Core.CharacterReentryTimeSeconds);
	}
}
