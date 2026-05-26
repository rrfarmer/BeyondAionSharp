using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAbnormalEffectFactResolverServiceTests
{
	[Fact]
	public void Resolve_UsesPlayerAbnormalMaskAndFiltersNoShowToggleLikeEffectController()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();
		var player = CreatePlayer();
		player.AbnormalState = PlayerAbnormalState.Root | PlayerAbnormalState.Poison;

		var result = service.Resolve(
			player,
			[
				CreateEntry(skillId: 3001, targetSlotId: 1, targetSlotOrdinal: 0, remainingTime: 45_000),
				CreateEntry(skillId: 3002, targetSlotId: 64, targetSlotOrdinal: 6, remainingTime: -1, isNoShowToggle: true),
			]);

		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot, result.Status);
		Assert.NotNull(result.Facts);
		Assert.Equal((int)(PlayerAbnormalState.Root | PlayerAbnormalState.Poison), result.Facts.AbnormalEffectMask);
		Assert.Equal(SmAbnormalEffect.FullSkillTargetSlots, result.Facts.Slots);
		var effect = Assert.Single(result.Facts.Effects);
		Assert.Equal(3001, effect.SkillId);
		Assert.Equal(45_000, effect.RemainingTimeToDisplayMillis);
		Assert.True(result.NeedsJavaEffectControllerParity);
		Assert.False(result.IsLive);
		Assert.False(result.IsJavaEffectControllerParity);
		Assert.Contains("EffectController.getAbnormals", result.JavaSource);
	}

	[Fact]
	public void Resolve_FiltersEntriesByRequestedSlots()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();
		var player = CreatePlayer();

		var result = service.Resolve(
			player,
			[
				CreateEntry(skillId: 3001, targetSlotId: 1, targetSlotOrdinal: 0, remainingTime: 45_000),
				CreateEntry(skillId: 3002, targetSlotId: 2, targetSlotOrdinal: 1, remainingTime: 90_000),
			],
			slots: 2);

		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot, result.Status);
		var effect = Assert.Single(result.Facts!.Effects);
		Assert.Equal(3002, effect.SkillId);
		Assert.Equal(2, result.Facts.Slots);
	}

	[Fact]
	public void Resolve_PreservesInputOrderAfterFiltering()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();
		var player = CreatePlayer();

		var result = service.Resolve(
			player,
			[
				CreateEntry(skillId: 3001, targetSlotId: 1, targetSlotOrdinal: 0, remainingTime: 45_000),
				CreateEntry(skillId: 3002, targetSlotId: 64, targetSlotOrdinal: 6, remainingTime: -1, isNoShowToggle: true),
				CreateEntry(skillId: 3003, targetSlotId: 2, targetSlotOrdinal: 1, remainingTime: 90_000),
				CreateEntry(skillId: 3004, targetSlotId: 4, targetSlotOrdinal: 2, remainingTime: 120_000),
			]);

		Assert.Equal([3001, 3003, 3004], result.Facts!.Effects.Select(effect => effect.SkillId));
	}

	[Fact]
	public void Resolve_KeepsNonToggleNoShowEntriesForPacketSource()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();
		var player = CreatePlayer();

		var result = service.Resolve(
			player,
			[CreateEntry(skillId: 3001, targetSlotId: 64, targetSlotOrdinal: 6, remainingTime: 45_000)]);

		var effect = Assert.Single(result.Facts!.Effects);
		Assert.Equal(3001, effect.SkillId);
		Assert.Equal(64, effect.TargetSlotId);
		Assert.Equal(6, effect.TargetSlotOrdinal);
	}

	[Fact]
	public void Resolve_PreservesSnapshotRemainingTimeSentinels()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();
		var player = CreatePlayer();

		var result = service.Resolve(
			player,
			[CreateEntry(skillId: 3001, targetSlotId: 1, targetSlotOrdinal: 0, remainingTime: -1)]);

		Assert.Equal(-1, Assert.Single(result.Facts!.Effects).RemainingTimeToDisplayMillis);
		Assert.Contains("Remaining-time values are passed through", result.Notes);
	}

	[Fact]
	public void Resolve_MissingInputsReturnExplicitBlockedMetadata()
	{
		var service = new PlayerKnownListAbnormalEffectFactResolverService();

		var missingPlayer = service.Resolve(player: null, []);
		var missingEffects = service.Resolve(CreatePlayer(), effects: null);

		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.MissingPlayer, missingPlayer.Status);
		Assert.Null(missingPlayer.Facts);
		Assert.True(missingPlayer.NeedsJavaEffectControllerParity);
		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.MissingEffectSnapshot, missingEffects.Status);
		Assert.Null(missingEffects.Facts);
		Assert.True(missingEffects.NeedsJavaEffectControllerParity);
	}

	private static Player CreatePlayer() =>
		new()
		{
			ObjectId = 9001,
			Race = "ELYOS",
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
		};

	private static PlayerKnownListAbnormalEffectSnapshotEntry CreateEntry(
		int skillId,
		int targetSlotId,
		int targetSlotOrdinal,
		int remainingTime,
		bool isNoShowToggle = false) =>
		new(
			EffectorObjectId: 7001,
			skillId,
			SkillLevel: 3,
			targetSlotId,
			targetSlotOrdinal,
			remainingTime,
			isNoShowToggle);
}
