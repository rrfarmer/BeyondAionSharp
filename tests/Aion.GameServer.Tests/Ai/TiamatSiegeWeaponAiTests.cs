using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Iron Wall Warfront siege weapons, which left nothing behind when destroyed.
/// </summary>
/// <remarks>
/// See <see cref="TiamatSiegeWeaponAI"/>. All eleven ran plain <c>aggressive</c>, so the
/// <c>on_killed_by_user</c> rung that hands the raid a usable cannon could not have fired.
/// <para>
/// <b>These raise the death event rather than calling <c>BossAiHarness.Kill</c>, and the reason is
/// specific.</b> Retail guards the rung with <c>is_user</c>, which this port reads as "a player did the
/// most damage" — and <c>Kill</c> deliberately records no damage, because the reward path it would
/// otherwise run wants the housing service and a database. So the two cannot both be had: <c>Kill</c>
/// proves the controller reaches a death branch (<see cref="HarnessKillTests"/>), and these prove what
/// the branch does when a player is the one who earned it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatSiegeWeaponAiTests
{
	private const int IronWallWarfront = 301220000;

	/// <summary>One cannon and one direct gun, with retail's own replacement and heading.</summary>
	public static TheoryData<int, int, int> Weapons() => new TheoryData<int, int, int>
	{
		{ 233742, 284869, 90 },    // IDF5_TD_War_Vri_Cannon_03
		{ 233747, 284874, 150 },   // IDF5_TD_War_Vri_DirectGun_03
		{ 233745, 284872, 0 },     // IDF5_TD_War_Vri_Cannon_06, whose dir is zero and must stay zero
	};

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(IronWallWarfront).WithWorldSize(2048)
			.WithAi(typeof(TiamatSiegeWeaponAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Destroys a weapon the way a raid does: a player does the damage, then it dies.</summary>
	private static Npc DestroyedByPlayer(BossAiHarness harness, int npcId)
	{
		Npc weapon = harness.Spawn(npcId, 500f, 500f, 200f);
		Player raider = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(weapon, raider);
		BossAiHarness.Wound(weapon, raider);
		weapon.GetAi().OnGeneralEvent(AiEventType.Died);
		return weapon;
	}

	/// <summary>
	/// <b>Each destroyed weapon leaves its own usable one.</b>
	/// </summary>
	[Theory]
	[MemberData(nameof(Weapons))]
	public void EachWeaponLeavesItsOwnReplacement(int weaponId, int replacementId, int degrees)
	{
		_ = degrees;
		using BossAiHarness harness = NewHarness();
		DestroyedByPlayer(harness, weaponId);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == replacementId));
	}

	/// <summary>
	/// <b>And it faces the way retail points it.</b>
	/// </summary>
	/// <remarks>
	/// The pin that needed <c>Do.SpawnFacing</c> to exist. <c>SpawnNear</c> hands over the spawner's
	/// heading, which would leave every replacement pointing wherever the destroyed one happened to face
	/// — and the eleven dirs retail gives are all different.
	/// </remarks>
	[Theory]
	[MemberData(nameof(Weapons))]
	public void AndItFacesTheWayRetailPointsIt(int weaponId, int replacementId, int degrees)
	{
		using BossAiHarness harness = NewHarness();
		DestroyedByPlayer(harness, weaponId);

		Npc replacement = harness.LiveNpcs().Single(n => n.GetNpcId() == replacementId);
		Assert.Equal((byte)(degrees / 3), replacement.GetHeading());
	}

	/// <summary>
	/// <b>A weapon that dies with no player damage leaves nothing.</b> Retail's <c>is_user</c>.
	/// </summary>
	/// <remarks>
	/// Without this the guard is invisible: every other pin here supplies a player, so removing
	/// <c>When.KilledByPlayer</c> entirely would leave them all green while a reset littered the field
	/// with artillery nobody earned.
	/// </remarks>
	[Fact]
	public void AWeaponThatFallsWithoutAPlayerLeavesNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc weapon = harness.Spawn(233742, 500f, 500f, 200f);

		weapon.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == 284869));
	}

	/// <summary>
	/// <b>Every weapon in the table leaves something different.</b>
	/// </summary>
	/// <remarks>
	/// Eleven rows copied by hand from eleven patterns is exactly where a paste error lands, and a
	/// duplicated replacement id would be invisible in the per-weapon pins above.
	/// </remarks>
	[Fact]
	public void NoTwoWeaponsLeaveTheSameReplacement()
	{
		Assert.Equal(TiamatSiegeWeaponAI.Replacements.Count,
			TiamatSiegeWeaponAI.Replacements.Values.Select(v => v.Replacement).Distinct().Count());
	}
}
