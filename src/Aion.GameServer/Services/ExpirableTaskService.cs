using System.Collections.Concurrent;
using System.Globalization;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class ExpirableTaskService
{
	private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(500);
	private static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

	private readonly ConcurrentDictionary<int, PlayerExpirableRegistration> _registrations = new();
	private readonly IPlayerEnterWorldRepository _repository;
	private readonly ILogger<ExpirableTaskService> _logger;
	private readonly Func<DateTimeOffset> _clock;

	public ExpirableTaskService(
		ThreadPoolManager threadPoolManager,
		IPlayerEnterWorldRepository repository,
		ILogger<ExpirableTaskService> logger)
		: this(threadPoolManager, repository, logger, () => DateTimeOffset.UtcNow)
	{
	}

	public ExpirableTaskService(
		ThreadPoolManager threadPoolManager,
		IPlayerEnterWorldRepository repository,
		ILogger<ExpirableTaskService> logger,
		Func<DateTimeOffset> clock)
	{
		// Java parity: taskmanager/tasks/ExpireTimerTask extends AbstractPeriodicTaskManager(1000).
		_repository = repository;
		_logger = logger;
		_clock = clock;
		_ = threadPoolManager.ScheduleAtFixedRate(
			_ => new ValueTask(TickAsync()),
			InitialDelay,
			Period);
	}

	public void RegisterPlayerExpirables(
		Player player,
		Func<GameServerPacket, Task> sendPacketAsync,
		Func<GameServerPacket, Task>? broadcastVisibleAsync = null,
		TitleTemplateTable? titleTemplates = null)
	{
		// Java parity: services/player/PlayerEnterWorldService registers loaded motions, emotions, and titles.
		UnregisterPlayer(player);
		var registration = new PlayerExpirableRegistration(player, sendPacketAsync, broadcastVisibleAsync, titleTemplates);
		_registrations[player.ObjectId] = registration;
		foreach (var emotion in player.Emotions)
			AddExpirable(registration, ExpirableKind.Emotion, emotion.Id, emotion.ExpireTimeSeconds);
		foreach (var title in player.Titles)
			AddExpirable(registration, ExpirableKind.Title, title.Id, title.ExpireTimeSeconds);
		foreach (var motion in player.Motions)
			AddExpirable(registration, ExpirableKind.Motion, motion.Id, motion.ExpireTimeSeconds);
	}

	public void RegisterEmotion(Player player, PlayerEmotion emotion)
	{
		// Java parity: model/gameobjects/player/emotion/EmotionList.add(..., isNew=true).
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Emotion, emotion.Id, emotion.ExpireTimeSeconds);
	}

	public void RegisterTitle(Player player, PlayerTitle title)
	{
		// Java parity: model/gameobjects/player/title/TitleList.addTitle registers temporary titles.
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Title, title.Id, title.ExpireTimeSeconds);
	}

	public void RegisterMotion(Player player, PlayerMotion motion)
	{
		// Java parity: model/gameobjects/player/motion/MotionList.add(..., persist=true).
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Motion, motion.Id, motion.ExpireTimeSeconds);
	}

	public void UnregisterPlayer(Player player)
	{
		// Java parity: services/player/PlayerLeaveWorldService -> ExpireTimerTask.unregisterExpirables.
		_registrations.TryRemove(player.ObjectId, out _);
	}

	public async Task TickAsync(CancellationToken cancellationToken = default)
	{
		var now = _clock();
		foreach (var registration in _registrations.Values.ToArray())
		{
			foreach (var expirable in registration.Expirables.Keys.ToArray())
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				var remainingSeconds = expirable.ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
				if (remainingSeconds >= 0)
					continue;

				if (!_registrations.TryGetValue(registration.Player.ObjectId, out var currentRegistration)
					|| !ReferenceEquals(currentRegistration, registration)
					|| !registration.Expirables.TryRemove(expirable, out _))
				{
					continue;
				}

				await ExpireAsync(registration, expirable, cancellationToken);
			}
		}
	}

	private static void AddExpirable(
		PlayerExpirableRegistration registration,
		ExpirableKind kind,
		int id,
		int expireTimeSeconds)
	{
		if (expireTimeSeconds <= 0)
			return;

		registration.Expirables.TryAdd(new ExpirableKey(kind, id, expireTimeSeconds), 0);
	}

	private async Task ExpireAsync(
		PlayerExpirableRegistration registration,
		ExpirableKey expirable,
		CancellationToken cancellationToken)
	{
		try
		{
			switch (expirable.Kind)
			{
				case ExpirableKind.Emotion:
					await ExpireEmotionAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.Title:
					await ExpireTitleAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.Motion:
					await ExpireMotionAsync(registration, expirable.Id, cancellationToken);
					break;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not expire {ExpirableKind} {ExpirableId} for player {PlayerObjectId}", expirable.Kind, expirable.Id, registration.Player.ObjectId);
		}
	}

	private async Task ExpireEmotionAsync(
		PlayerExpirableRegistration registration,
		int emotionId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/emotion/Emotion.onExpire -> EmotionList.remove.
		var player = registration.Player;
		var removed = false;
		lock (registration.SyncRoot)
		{
			if (player.Emotions.Any(emotion => emotion.Id == emotionId))
			{
				player.Emotions = player.Emotions.Where(emotion => emotion.Id != emotionId).ToArray();
				removed = true;
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerEmotionAsync(player.ObjectId, emotionId, cancellationToken);
		await registration.SendPacketAsync(new SmEmotionList(0, player.Emotions));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashSocialActionByTimeout());
	}

	private async Task ExpireTitleAsync(
		PlayerExpirableRegistration registration,
		int titleId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/title/Title.onExpire -> TitleList.removeTitle.
		var player = registration.Player;
		var removed = false;
		var removedDisplayTitle = false;
		var removedBonusTitle = false;
		lock (registration.SyncRoot)
		{
			if (player.Titles.Any(title => title.Id == titleId))
			{
				player.Titles = player.Titles.Where(title => title.Id != titleId).ToArray();
				removed = true;
				if (player.TitleId == titleId)
				{
					player.TitleId = -1;
					removedDisplayTitle = true;
				}

				if (player.BonusTitleId == titleId)
				{
					player.BonusTitleId = -1;
					removedBonusTitle = true;
				}
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerTitleAsync(player.ObjectId, titleId, cancellationToken);
		if (removedDisplayTitle)
		{
			await registration.SendPacketAsync(new SmTitleInfo(-1));
			if (registration.BroadcastVisibleAsync != null)
				await registration.BroadcastVisibleAsync(new SmTitleInfo(player, -1));
		}

		if (removedBonusTitle)
			await registration.SendPacketAsync(new SmTitleInfo(6, -1));

		await registration.SendPacketAsync(new SmTitleInfo(player.Titles));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashTitleByTimeout(GetTitleName(registration, titleId)));
	}

	private async Task ExpireMotionAsync(
		PlayerExpirableRegistration registration,
		int motionId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/motion/Motion.onExpire -> MotionList.remove.
		var player = registration.Player;
		var removed = false;
		lock (registration.SyncRoot)
		{
			if (player.Motions.Any(motion => motion.Id == motionId))
			{
				player.Motions = player.Motions.Where(motion => motion.Id != motionId).ToArray();
				removed = true;
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerMotionAsync(player.ObjectId, motionId, cancellationToken);
		await registration.SendPacketAsync(SmMotion.Remove(motionId));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashCustomAnimationByTimeout());
	}

	private static string GetTitleName(PlayerExpirableRegistration registration, int titleId)
	{
		var titleTemplate = registration.TitleTemplates?.GetTitleTemplate(titleId);
		return titleTemplate == null
			? titleId.ToString(CultureInfo.InvariantCulture)
			: ChatUtil.L10n(titleTemplate.NameId);
	}

	private sealed class PlayerExpirableRegistration
	{
		public PlayerExpirableRegistration(
			Player player,
			Func<GameServerPacket, Task> sendPacketAsync,
			Func<GameServerPacket, Task>? broadcastVisibleAsync,
			TitleTemplateTable? titleTemplates)
		{
			Player = player;
			SendPacketAsync = sendPacketAsync;
			BroadcastVisibleAsync = broadcastVisibleAsync;
			TitleTemplates = titleTemplates;
		}

		public Player Player { get; }

		public Func<GameServerPacket, Task> SendPacketAsync { get; }

		public Func<GameServerPacket, Task>? BroadcastVisibleAsync { get; }

		public TitleTemplateTable? TitleTemplates { get; }

		public object SyncRoot { get; } = new();

		public ConcurrentDictionary<ExpirableKey, byte> Expirables { get; } = new();
	}

	private readonly record struct ExpirableKey(ExpirableKind Kind, int Id, int ExpireTimeSeconds);

	private enum ExpirableKind
	{
		Emotion,
		Title,
		Motion,
	}
}
