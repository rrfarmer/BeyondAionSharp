using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Grand Commander Pashid spawned the rank-and-file rider's strike instead of his own.
/// </summary>
/// <remarks>
/// The Eternal Bastion's fifth wave has two strike npcs and they are one apart:
/// <list type="bullet">
/// <item><c>284697</c> "pashid siege dragon" — <c>BIDF5_TD_DragonRiderStrike</c>, spawned by the
/// ordinary <c>IDF5_TD_DragonRider_N_65_Ae</c> and by the wave's four summon patterns.</item>
/// <item><c>284698</c> "grand commander pashid" — <c>BIDF5_TD_DragonRiderChiefStrike</c>, spawned by
/// <c>IDF5_TD_Wave5_Boss</c>, which is Pashid's own pattern.</item>
/// </list>
/// This class had 284697. It is the fifth row read off the ranked audit and the first that turned out to
/// be owed rather than explained.
/// <para>
/// <b>It is pinned as a constant rather than by watching a spawn, because the spawn is inert.</b> Both
/// strike npcs run <c>useSkillAndDie</c>, which deletes the npc when its skill list is empty, and neither
/// has an <c>npc_skills</c> entry in our data. A behavioural pin would assert that nothing appears — and
/// would keep passing if the id were wrong again.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PashidChiefStrikeTests
{
	/// <summary>
	/// <b>He spawns the chief's strike.</b> The number is written out rather than read from the class: a
	/// pin that takes its expectation from the constant it is pinning cannot see that constant change,
	/// and the whole defect here was a single digit.
	/// </summary>
	[Fact]
	public void HeSpawnsTheChiefsStrikeNotTheRiders()
	{
		Assert.Equal(284698, EternalBastionCommanderPashidAI.ChiefStrike);
		Assert.NotEqual(284697, EternalBastionCommanderPashidAI.ChiefStrike);
	}
}
