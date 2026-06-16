using Xunit;

namespace Aion.GameServer.Tests;

/// <summary>
/// Serializes the golden test classes that mutate the global <c>DataManager</c> singleton via
/// <c>DataManager.RegisterInstance(...)</c>. xUnit runs distinct test classes in parallel by default,
/// so without this they raced on the shared singleton — a class that registered a DataManager without
/// PLAYER_EXPERIENCE_TABLE could win the race mid-run and make GoldenStatsInfoFixtureTests.GetExpNeed NRE.
/// Putting them all in one collection with parallelization disabled removes that race.
/// </summary>
[CollectionDefinition("GoldenDataManager", DisableParallelization = true)]
public sealed class GoldenDataManagerCollection
{
}
