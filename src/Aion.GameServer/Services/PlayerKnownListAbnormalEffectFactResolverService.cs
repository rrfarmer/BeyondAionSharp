using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListAbnormalEffectFactResolutionStatus
{
	ResolvedSnapshot,
	MissingPlayer,
	MissingEffectSnapshot,
}

public sealed record PlayerKnownListAbnormalEffectSnapshotEntry(
	int EffectorObjectId,
	int SkillId,
	int SkillLevel,
	int TargetSlotId,
	int TargetSlotOrdinal,
	int RemainingTimeToDisplayMillis,
	bool IsNoShowToggle = false);

public sealed record PlayerKnownListAbnormalEffectFacts(
	IReadOnlyList<SmAbnormalEffectEntry> Effects,
	int AbnormalEffectMask,
	int Slots);

public sealed record PlayerKnownListAbnormalEffectFactResolution(
	PlayerKnownListAbnormalEffectFactResolutionStatus Status,
	PlayerKnownListAbnormalEffectFacts? Facts,
	bool NeedsJavaEffectControllerParity,
	bool IsLive,
	bool IsJavaEffectControllerParity,
	string JavaSource,
	string Notes);

public sealed class PlayerKnownListAbnormalEffectFactResolverService
{
	public PlayerKnownListAbnormalEffectFactResolution Resolve(
		Player? player,
		IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>? effects,
		int slots = SmAbnormalEffect.FullSkillTargetSlots)
	{
		// Java parity breadcrumb: EffectController.getAbnormals(),
		// EffectController.getAbnormalEffects(), and SM_ABNORMAL_EFFECT(Creature)
		// read a live StampedLock-protected effect map. This resolver only
		// normalizes caller-supplied snapshots and never touches live controller state.
		const string javaSource =
			"com.aionemu.gameserver.controllers.effect.EffectController.getAbnormals/getAbnormalEffects; "
			+ "com.aionemu.gameserver.network.aion.serverpackets.SM_ABNORMAL_EFFECT";

		if (player is null)
		{
			return new PlayerKnownListAbnormalEffectFactResolution(
				PlayerKnownListAbnormalEffectFactResolutionStatus.MissingPlayer,
				Facts: null,
				NeedsJavaEffectControllerParity: true,
				IsLive: false,
				IsJavaEffectControllerParity: false,
				javaSource,
				"No player snapshot was supplied for abnormal-effect fact resolution.");
		}

		if (effects is null)
		{
			return new PlayerKnownListAbnormalEffectFactResolution(
				PlayerKnownListAbnormalEffectFactResolutionStatus.MissingEffectSnapshot,
				Facts: null,
				NeedsJavaEffectControllerParity: true,
				IsLive: false,
				IsJavaEffectControllerParity: false,
				javaSource,
				"Effect entries are still caller-supplied; live EffectController abnormal-effect map hydration is not ported.");
		}

		var filtered = effects
			.Where(effect => !effect.IsNoShowToggle)
			.Where(effect => slots == SmAbnormalEffect.FullSkillTargetSlots || (slots & effect.TargetSlotId) != 0)
			.Select(effect => new SmAbnormalEffectEntry(
				effect.EffectorObjectId,
				effect.SkillId,
				effect.SkillLevel,
				effect.TargetSlotId,
				effect.TargetSlotOrdinal,
				effect.RemainingTimeToDisplayMillis))
			.ToArray();

		return new PlayerKnownListAbnormalEffectFactResolution(
			PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot,
			new PlayerKnownListAbnormalEffectFacts(filtered, (int)player.AbnormalState, slots),
			NeedsJavaEffectControllerParity: true,
			IsLive: false,
			IsJavaEffectControllerParity: false,
			javaSource,
			"Resolved abnormal-effect packet facts from supplied snapshot entries only. Remaining-time values are passed through from the snapshot; Java timer/endTime calculation and StampedLock map ordering are not hydrated.");
	}
}
