using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the shulack mercenaries of the Danuar Sanctuary, translated from the eleven
/// <c>IDF5_U2_*</c> patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>Every npc here is spawned with its class named explicitly.</b> These pins were the reason an
/// encounter was reverted unshipped for two entries, and the cause was in this file rather than in the
/// classes: a probe used <c>Spawn</c>, which reads the AI name off the npc template, at a moment when
/// the template repoint had been rolled back — so the "watcher" under test was a stock aggressive npc
/// that had never heard of the pattern. <c>SpawnWithAi</c> cannot go wrong that way.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ShulackMercenaryAiTests
{
	private const int DanuarSanctuary = 301500000;

	private const int Chief = 235654;          // sends 21251 + 21253 at 50m when pulled
	private const int BodyguardAlarm = 235656; // sends 21253 only
	private const int BodyguardBoth = 235655;  // answers 21251 with a thousand
	private const int Watcher = 235565;        // relays 21253 a second later
	private const int Assaulter = 235569;      // relays 21153 -- retail's typo
	private const int CannonChief = 235574;    // sends 21271 at 15m
	private const int Soldier = 235566;        // answers 21271
	private const int Slave = 235589;          // answers 21253

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DanuarSanctuary).WithWorldSize(2048)
			.WithAi(typeof(ShulackChiefAI), typeof(ShulackBodyguardAlarmAI),
				typeof(ShulackBodyguardBothAI), typeof(ShulackWatcherAI), typeof(ShulackAssaulterAI),
				typeof(ShulackCannonChiefAI), typeof(ShulackSoldierAI), typeof(ShulackSlaveAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>An officer's call is worth a thousand and everything else a hundred.</b> Two ranks, two
	/// payloads, and the difference is the whole tiering of the camp.
	/// </summary>
	[Fact]
	public void AnOfficersCallIsWorthTenOfAnyoneElses()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.SpawnWithAi(Chief, "shulack_chief", 300f, 300f, 200f);
		Npc bodyguard = harness.SpawnWithAi(BodyguardBoth, "shulack_bodyguard_both", 306f, 300f, 200f);
		Npc slave = harness.SpawnWithAi(Slave, "shulack_slave", 308f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(chief, bodyguard);
		BossAiHarness.MakeMutuallyKnown(chief, slave);

		harness.Engage(chief, raider);

		Assert.Equal(1000, bodyguard.GetAggroList().GetHate(raider));
		Assert.Equal(100, slave.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The alarm relays.</b> A watcher that hears it takes its hundred and passes the alarm on a
	/// second later — so a slave the caller has never seen is pulled in anyway. This is the first relay
	/// in the log; every other call reaches only its own circle.
	/// </summary>
	[Fact]
	public void TheAlarmRelaysThroughTheWatchers()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.SpawnWithAi(BodyguardAlarm, "shulack_bodyguard_alarm", 300f, 300f, 200f);
		Npc watcher = harness.SpawnWithAi(Watcher, "shulack_watcher", 304f, 300f, 200f);
		Npc slave = harness.SpawnWithAi(Slave, "shulack_slave", 344f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, watcher);
		BossAiHarness.MakeMutuallyKnown(watcher, slave);
		BossAiHarness.MakeMutuallyKnown(slave, raider);

		// The relay rides a battle timer, and battle timers only run in combat.
		harness.Engage(watcher, raider);
		harness.Engage(caller, raider);

		// The slave is not in the caller's known list, so it hears nothing directly.
		Assert.Equal(0, slave.GetAggroList().GetHate(raider));

		harness.Watch(3, null);

		Assert.Equal(100, slave.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The assaulter's relay goes nowhere, and that is retail's.</b> It is the watcher with one digit
	/// changed — <c>21153</c> instead of <c>21253</c> — and <c>21153</c>'s only listener anywhere in the
	/// 5.8 files is <c>IDRuneWP_A3_Protection_65_n</c>, a rune-weapon pattern from a different instance.
	/// </summary>
	/// <remarks>
	/// Kept exactly as written: correcting it would be inventing a mechanic NCSoft does not ship.
	/// </remarks>
	[Fact]
	public void TheAssaultersRelayGoesNowhere()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.SpawnWithAi(BodyguardAlarm, "shulack_bodyguard_alarm", 300f, 300f, 200f);
		Npc assaulter = harness.SpawnWithAi(Assaulter, "shulack_assaulter", 304f, 300f, 200f);
		Npc slave = harness.SpawnWithAi(Slave, "shulack_slave", 344f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, assaulter);
		BossAiHarness.MakeMutuallyKnown(assaulter, slave);
		BossAiHarness.MakeMutuallyKnown(slave, raider);

		harness.Engage(assaulter, raider);
		int heard = assaulter.GetAggroList().GetHate(raider);
		harness.Engage(caller, raider);

		// It heard the alarm and took its hundred, exactly as the watcher does.
		Assert.Equal(heard + 100, assaulter.GetAggroList().GetHate(raider));

		harness.Watch(3, null);

		// And what it passes on reaches nothing: the slave never hears the alarm.
		Assert.Equal(0, slave.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And a watcher relays once.</b> Retail flags the alarm branch, so a second alarm reaching the
	/// same watcher is answered but not passed on.
	/// </summary>
	/// <remarks>
	/// <b>Measured with a slave that arrives after the first relay</b>, for the reason the Tiamat
	/// insurgents' pair of pins gives: a listener that already answered cannot show you a second relay,
	/// because its own hate is already there. A fresh one can only have heard a second.
	/// </remarks>
	[Fact]
	public void AndAWatcherRelaysOnlyOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.SpawnWithAi(BodyguardAlarm, "shulack_bodyguard_alarm", 300f, 300f, 200f);
		Npc second = harness.SpawnWithAi(BodyguardAlarm, "shulack_bodyguard_alarm", 301f, 300f, 200f);
		Npc watcher = harness.SpawnWithAi(Watcher, "shulack_watcher", 304f, 300f, 200f);
		Npc slave = harness.SpawnWithAi(Slave, "shulack_slave", 344f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(caller, watcher);
		BossAiHarness.MakeMutuallyKnown(second, watcher);
		BossAiHarness.MakeMutuallyKnown(watcher, slave);
		BossAiHarness.MakeMutuallyKnown(slave, raider);

		harness.Engage(watcher, raider);
		harness.Engage(caller, raider);
		harness.Watch(3, null);
		Assert.True(slave.GetAggroList().GetHate(raider) >= 100, "the first relay never landed");

		// A listener that has heard nothing yet: only a second relay could reach it.
		Npc fresh = harness.SpawnWithAi(Slave, "shulack_slave", 345f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(watcher, fresh);
		BossAiHarness.MakeMutuallyKnown(fresh, raider);

		harness.Engage(second, raider);
		harness.Watch(5, null);

		Assert.Equal(0, fresh.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The cannon chief calls the rank and file at fifteen metres</b>, the shortest reach in the
	/// family.
	/// </summary>
	[Fact]
	public void TheCannonChiefCallsTheRankAndFile()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.SpawnWithAi(CannonChief, "shulack_cannon_chief", 300f, 300f, 200f);
		Npc near = harness.SpawnWithAi(Soldier, "shulack_soldier", 306f, 300f, 200f);
		Npc far = harness.SpawnWithAi(Soldier, "shulack_soldier", 320f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(chief, near);
		BossAiHarness.MakeMutuallyKnown(chief, far);

		harness.Engage(chief, raider);

		Assert.Equal(100, near.GetAggroList().GetHate(raider));
		Assert.Equal(0, far.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Three numbers, three audiences.</b> A cannon chief's call leaves the slaves standing, and an
	/// alarm leaves the rank and file standing.
	/// </summary>
	[Fact]
	public void ThreeNumbersThreeAudiences()
	{
		using BossAiHarness harness = NewHarness();
		Npc cannonChief = harness.SpawnWithAi(CannonChief, "shulack_cannon_chief", 300f, 300f, 200f);
		Npc slave = harness.SpawnWithAi(Slave, "shulack_slave", 305f, 300f, 200f);
		Npc alarmCaller = harness.SpawnWithAi(BodyguardAlarm, "shulack_bodyguard_alarm", 400f, 300f, 200f);
		Npc soldier = harness.SpawnWithAi(Soldier, "shulack_soldier", 405f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		Player second = harness.SpawnPlayer(402f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(cannonChief, slave);
		BossAiHarness.MakeMutuallyKnown(alarmCaller, soldier);

		harness.Engage(cannonChief, raider);
		harness.Engage(alarmCaller, second);

		Assert.Equal(0, slave.GetAggroList().GetHate(raider));
		Assert.Equal(0, soldier.GetAggroList().GetHate(second));
	}

	/// <summary><b>The numbers and the ranges are retail's, not ours — including the typo.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(21251, ShulackCalls.Officers);
		Assert.Equal(21253, ShulackCalls.Alarm);
		Assert.Equal(21271, ShulackCalls.RankAndFile);
		Assert.Equal(21153, ShulackCalls.Mistyped);
		Assert.Equal(50f, ShulackCalls.Far);
		Assert.Equal(15f, ShulackCalls.Near);
	}
}
