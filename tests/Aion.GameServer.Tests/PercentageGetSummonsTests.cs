using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Ai;

namespace Aion.GameServer.Tests;

/// <summary>
/// Regression for the SummonerAI empty-list-vs-null gap: Percentage.GetSummons() must return null (not an empty
/// list) when no &lt;summonGroup&gt; is present, so SummonerAI's `getSummons() != null` branch matches Java/JAXB
/// (otherwise handleBeforeSpawn runs on a non-individual percent that has no summons).
/// </summary>
public sealed class PercentageGetSummonsTests
{
    [Fact]
    public void GetSummons_NullWhenUnsetOrEmpty_NonNullWhenPopulated()
    {
        var p = new Percentage();
        Assert.Null(p.GetSummons());                       // unset (as JAXB would leave it)

        p.summons = new List<SummonGroup>();               // empty (as C# XmlSerializer materializes it)
        Assert.Null(p.GetSummons());                       // normalized to null

        p.summons = new List<SummonGroup> { new SummonGroup() };
        Assert.NotNull(p.GetSummons());                    // real content stays non-null
        Assert.Single(p.GetSummons()!);
    }
}
