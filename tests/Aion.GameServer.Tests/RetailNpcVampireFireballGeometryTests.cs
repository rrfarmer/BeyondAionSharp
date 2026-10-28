using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Utils;
using G=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcVampireFireballGeometry;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcVampireFireballGeometryTests
{
    [SkippableFact]
    public void PinnedAreaSourceUsesStrictHorizontalRangeAndIndependentInclusiveHeight()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail skill sources");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x=>x.GetProperty("id").GetInt32()==18460);
        byte[] raw=File.ReadAllBytes(source.GetProperty("path").GetString()!);
        var geometry=G.ReadReviewedSource(raw);
        var changed=(byte[])raw.Clone();changed[0]^=1;
        Assert.Throws<InvalidDataException>(()=>G.ReadReviewedSource(changed));
        Assert.Equal(G.EnemyRestrictionResult.Rejected,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,10,2,0,0));
        Assert.Equal(G.EnemyRestrictionResult.EligibleForFurtherChecks,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,0,0));
        Assert.Equal(G.EnemyRestrictionResult.EligibleForFurtherChecks,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,0,1));
        Assert.Equal(G.EnemyRestrictionResult.Unresolved,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,0,2));
        Assert.Equal(G.EnemyRestrictionResult.Unresolved,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,0,-1));
        Assert.Equal(G.EnemyRestrictionResult.Unresolved,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,0,null));
        Assert.Equal(G.EnemyRestrictionResult.Unresolved,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,2,12,0));
        Assert.Equal(G.EnemyRestrictionResult.Unresolved,geometry.EvaluateOwnerlessNpcEnemyRestriction(10,11,1,0,0));
        var center=new G.Point(0,0,0);var caster=new G.Bounds(1,0.5f);var candidate=new G.Bounds(0.5f,1);
        Assert.Equal(G.Result.EligibleForFurtherChecks,geometry.Evaluate(center,caster,new(6.8f,0,6.5f),candidate));
        // The host's current center-to-center check is a 3D sphere even
        // after its full first-target and candidate radii are added.
        Assert.False(PositionUtil.IsInRange(0,0,0,6.8f,0,6.5f,9f));
        Assert.Equal(G.Result.EligibleForFurtherChecks,
            geometry.Evaluate(center,new(2,3),new(8.6f,0,0),new(0.2f,0.4f)));
        Assert.Equal(G.Result.OutsideGeometry,geometry.Evaluate(center,caster,new(8,0,0),candidate));
        Assert.Equal(G.Result.EligibleForFurtherChecks,geometry.Evaluate(center,caster,new(MathF.BitDecrement(8),0,7),candidate));
        Assert.Equal(G.Result.EligibleForFurtherChecks,geometry.Evaluate(center,caster,new(0,0,-7),candidate));
        Assert.Equal(G.Result.OutsideGeometry,geometry.Evaluate(center,caster,new(0,0,MathF.BitIncrement(7)),candidate));
        Assert.Equal(G.Result.Unrepresentable,geometry.Evaluate(center,caster,new(float.NaN,0,0),candidate));
        Assert.Equal(G.Result.Unrepresentable,geometry.Evaluate(center,new(-1,0),new(0,0,0),candidate));
        Assert.Equal(G.Result.Unrepresentable,geometry.Evaluate(center,caster,new(float.MaxValue,0,0),candidate));
        var first=new object();var others=Enumerable.Range(0,10).Select(_=>new object()).ToArray();
        var checkedActors=new List<object>();
        var selected=geometry.SelectFromReviewedOrder(first,[others[0],first,..others.Skip(1)],actor=>
        {
            checkedActors.Add(actor);return !ReferenceEquals(actor,others[2]);
        });
        Assert.Equal(8,selected.Count);Assert.Same(first,selected[0]);Assert.DoesNotContain(others[2],selected);
        Assert.Equal(new[]{first,others[0],others[1],others[3],others[4],others[5],others[6],others[7]},selected);
        // Retail still calls the restriction for remaining candidates after
        // the cap; skipping those calls would change the observed path.
        Assert.Equal(11,checkedActors.Count);Assert.Same(others[9],checkedActors[^1]);
        Assert.Throws<ArgumentException>(()=>geometry.SelectFromReviewedOrder(first,new object[]{null!},_=>true));
    }
}
