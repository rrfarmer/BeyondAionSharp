using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Aion.Simulation.Tests;

internal static class ThreeKeysRetailDiagnostics
{
    internal static bool IsVerifiedSourceRouteRejection(SimulationProblem problem) =>
        problem.Server == "gs" && problem.Level == LogLevel.Error && problem.ExceptionText == null &&
        Regex.IsMatch(problem.Message,
            @"\ARETAIL_SKYTEMPLE_WAYPOINT_REJECT npc=212775 object=[1-9][0-9]* route=B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B instance=[1-9][0-9]* reason=Waypoint 13 has no native path layer: point=RetailSearchPoint \{ X = 94\.2662, Y = 140\.98477, Z = 578\.75415, IsFinite = True \}; lookup=Complete; layer=0,type=14,status=NoGround,height=NaN\z",
            RegexOptions.CultureInvariant);
}

public sealed class ThreeKeysRetailDiagnosticTests
{
    private const string Known = "RETAIL_SKYTEMPLE_WAYPOINT_REJECT npc=212775 object=135191 route=B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B instance=2 reason=Waypoint 13 has no native path layer: point=RetailSearchPoint { X = 94.2662, Y = 140.98477, Z = 578.75415, IsFinite = True }; lookup=Complete; layer=0,type=14,status=NoGround,height=NaN";

    [Fact]
    public void AcceptsOnlyVerifiedSourceRejection()
    {
        SimulationProblem Problem(string message) => new("208f55be", "log", "gs", LogLevel.Error, message, null, null, null);
        Assert.True(ThreeKeysRetailDiagnostics.IsVerifiedSourceRouteRejection(Problem(Known)));
        foreach (var (before, after) in new[] { ("212775", "212776"), ("B123", "B124"),
                     ("Waypoint 13", "Waypoint 12"), ("578.75415", "578.75416"), ("NoGround", "NoLayer") })
            Assert.False(ThreeKeysRetailDiagnostics.IsVerifiedSourceRouteRejection(Problem(Known.Replace(before, after))));
        Assert.False(ThreeKeysRetailDiagnostics.IsVerifiedSourceRouteRejection(Problem(Known + " unexpected")));
        Assert.False(ThreeKeysRetailDiagnostics.IsVerifiedSourceRouteRejection(Problem(Known) with { ExceptionText = "failure" }));
    }
}
