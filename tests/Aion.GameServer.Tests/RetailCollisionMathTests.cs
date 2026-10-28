using System.Security.Cryptography;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailCollisionMathTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PrimitiveHasInclusiveBarycentricEdgesAndRetainsBothWindings(bool contiguous)
    {
        foreach (bool reversed in new[] { false, true })
        {
            var face = Triangle(CollisionFixture.Face(reversed: reversed));
            foreach (var origin in new RetailSearchPoint[] { new(.5f,.5f,1), new(1,1,1), new(0,0,1) })
            {
                var result = RetailCollisionMath.IntersectTriangle(face,origin,new(0,0,-2),contiguous);
                Assert.Equal(RetailCollisionStatus.Computed,result.Status); Assert.True(result.Hit);
                Assert.Equal(.5f,result.Parameter); Assert.False(result.RuntimeVerified);
            }
            var outside = RetailCollisionMath.IntersectTriangle(face,new(1.5f,1.5f,1),new(0,0,-2),contiguous);
            Assert.False(outside.Hit); Assert.Null(outside.Parameter);
            var parallel = RetailCollisionMath.IntersectTriangle(face,new(.5f,.5f,1),new(1,0,0),contiguous);
            Assert.False(parallel.Hit);
        }
    }

    [Theory]
    [InlineData(1,1,-1)] [InlineData(2,-1,2)] [InlineData(0,1,0)] [InlineData(1,-1,1)]
    public void PrimitiveReturnsRayParameterWithoutInventingSegmentAdmission(float originZ,float deltaZ,float expected)
    {
        var result=RetailCollisionMath.IntersectTriangle(Triangle(CollisionFixture.Face()),new(.25f,.25f,originZ),new(0,0,deltaZ),false);
        Assert.True(result.Hit); Assert.Equal(expected,result.Parameter);
    }

    [Fact]
    public void VerticalProjectionParameterIsWorldZAndNotAFloatReturnAbiGuess()
    {
        var result=RetailCollisionMath.IntersectTriangle(Triangle(CollisionFixture.Face(20)),new(.25f,.25f,0),new(0,0,1),false);
        Assert.True(result.Hit); Assert.Equal(20f,result.Parameter);
    }

    [Theory]
    [InlineData(.000999f,false)] [InlineData(.001f,true)] [InlineData(-.000999f,false)] [InlineData(-.001f,true)]
    public void DeterminantThresholdIsOpenAndTwoSided(float edgeY,bool expected)
    {
        var face=new RetailCollisionTriangle(0,new(0,0,0),new(1,0,0),new(0,edgeY,0),0,0);
        var result=RetailCollisionMath.IntersectTriangle(face,new(.25f,edgeY*.25f,1),new(0,0,-1),false);
        Assert.Equal(RetailCollisionStatus.Computed,result.Status); Assert.Equal(expected,result.Hit);
    }

    [Fact]
    public void EligibilityUsesOrientedNormalAndAnExplicitCosineReturnReceipt()
    {
        var up=RetailCollisionMath.GroundEligibility(Triangle(CollisionFixture.Face()),CollisionFixture.Cosine);
        var down=RetailCollisionMath.GroundEligibility(Triangle(CollisionFixture.Face(reversed:true)),CollisionFixture.Cosine);
        Assert.True(up.Eligible); Assert.False(down.Eligible); Assert.True(up.CosineEvidenceConsumed);
        var degenerate=RetailCollisionMath.GroundEligibility(new(0,new(0,0,0),new(0,0,0),new(0,0,0),0,0),CollisionFixture.Cosine);
        Assert.Equal(RetailCollisionStatus.Computed,degenerate.Status); Assert.False(degenerate.Eligible);
    }

    [Theory]
    [InlineData("null")] [InlineData("unknown")] [InlineData("pin")] [InlineData("function")]
    [InlineData("argument")] [InlineData("revision")] [InlineData("capture")] [InlineData("zero")] [InlineData("one")]
    public void CosineUnknownOrUnboundCannotBecomeAComputedSlopeDecision(string invalid)
    {
        RetailCollisionCosineReceipt? receipt=invalid switch
        {
            "null"=>null,"unknown"=>CollisionFixture.Cosine with {Known=false},"pin"=>CollisionFixture.Cosine with {NativeBinarySha256=new string('0',64)},
            "function"=>CollisionFixture.Cosine with {NativeFunction="0x0"},"argument"=>CollisionFixture.Cosine with {ArgumentBits=0},
            "revision"=>CollisionFixture.Cosine with {Revision=""},"capture"=>CollisionFixture.Cosine with {CaptureSha256="unknown"},
            "zero"=>CollisionFixture.Cosine with {ResultBits=0},_=>CollisionFixture.Cosine with {ResultBits=0x3f800000}
        };
        var result=RetailCollisionMath.GroundEligibility(Triangle(CollisionFixture.Face()),receipt);
        Assert.Equal(RetailCollisionStatus.MissingCosineEvidence,result.Status); Assert.Null(result.Eligible);
    }

    [Theory]
    [InlineData(0x7fc01234u)] [InlineData(0x7f800000u)] [InlineData(1u)]
    public void ConsumedUnsupportedFloatStatesRejectWithoutFalseGeometry(uint bits)
    {
        float value=BitConverter.Int32BitsToSingle(unchecked((int)bits));
        var result=RetailCollisionMath.IntersectTriangle(Triangle(CollisionFixture.Face()),new(value,.5f,1),new(0,0,-2),false);
        Assert.Equal(RetailCollisionStatus.UnsupportedArithmetic,result.Status); Assert.Null(result.Hit); Assert.Null(result.Parameter);
        var cosine=RetailCollisionMath.GroundEligibility(Triangle(CollisionFixture.Face()),CollisionFixture.Cosine with {ResultBits=bits});
        Assert.Equal(RetailCollisionStatus.UnsupportedArithmetic,cosine.Status); Assert.Null(cosine.Eligible);
    }

    [Fact]
    public void SignedZeroIsAdmittedButOverflowingIntermediateArithmeticIsNot()
    {
        var signed=RetailCollisionMath.IntersectTriangle(Triangle(CollisionFixture.Face()),new(-0f,0,1),new(0,0,-2),false);
        Assert.True(signed.Hit);
        var overflow=new RetailCollisionTriangle(0,new(-float.MaxValue,0,0),new(float.MaxValue,0,0),new(0,1,0),0,0);
        var result=RetailCollisionMath.IntersectTriangle(overflow,new(0,0,1),new(0,0,-2),false);
        Assert.Equal(RetailCollisionStatus.UnsupportedArithmetic,result.Status); Assert.Null(result.Hit);
    }

    [Theory]
    [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(4,false)] [InlineData(5,false)]
    [InlineData(6,false)] [InlineData(7,false)] [InlineData(8,false)] [InlineData(9,false)]
    [InlineData(11,true)] [InlineData(13,true)] [InlineData(14,true)] [InlineData(15,true)] [InlineData(16,true)]
    [InlineData(135,true)] [InlineData(136,true)]
    public void ClassifiedMaterialsTagWithoutConsumingAnUnobservedCosine(byte material,bool special)
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face(material:material)]),
            CollisionFixture.Collision((x,y)=>x==0&&y==0?[new(0,0)]:[new(0)]));
        var result=RetailCollisionMath.Prepare(raw,null,CollisionFixture.PrepareLimits);
        Assert.True(result.Success,result.Detail); Assert.False(result.CosineEvidenceConsumed);
        Assert.Equal(0x80000000u,Assert.Single(result.Data!.References)); Assert.Equal(special,result.Data.Cells[0].SpecialMaterial);
        Assert.Equal(special,result.Data.HasSpecialMaterial);
    }

    [Fact]
    public void PreprocessOrsTagsAndSortsEachBandWithoutRewritingRawBytesOrReversingBands()
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face(),CollisionFixture.Face(reversed:true),CollisionFixture.Face(),CollisionFixture.Face(material:11)]),
            CollisionFixture.Collision((x,y)=>x==0&&y==0?[new(4,3,0x80000002u,0,1),new(2,2)]:[new(0)]));
        byte[] before=raw.CollisionBytes.ToArray();
        var result=RetailCollisionMath.Prepare(raw,CollisionFixture.Cosine,CollisionFixture.PrepareLimits);
        Assert.True(result.Success,result.Detail); Assert.True(result.CosineEvidenceConsumed);
        Assert.Equal(new uint[]{0,0x80000001u,0x80000002u,0x80000003u,2},result.Data!.References);
        Assert.Equal(new short[]{4,2},result.Data.Bands.Take(2).Select(b=>b.Height));
        Assert.True(result.Data.Cells[0].SpecialMaterial); Assert.Equal(before,raw.CollisionBytes.ToArray());
        Assert.Equal(3u,raw.GetRawReference(raw.Bands[0],0)); Assert.Equal(0x80000002u,raw.GetRawReference(raw.Bands[0],1));
    }

    [Fact]
    public void UnclassifiedReferencedFaceRequiresCosineButUnreferencedFacesDoNot()
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()]);
        var unused=RetailCollisionMath.Prepare(CollisionFixture.Read(mesh,CollisionFixture.Collision()),null,CollisionFixture.PrepareLimits);
        Assert.True(unused.Success); Assert.False(unused.CosineEvidenceConsumed);
        var used=RetailCollisionMath.Prepare(CollisionFixture.Read(mesh,CollisionFixture.Collision((x,y)=>[new(0,0)])),null,CollisionFixture.PrepareLimits);
        Assert.Equal(RetailCollisionStatus.MissingCosineEvidence,used.Status); Assert.Null(used.Data);
    }

    [Theory]
    [InlineData("references")] [InlineData("bands")] [InlineData("metadata")]
    public void PreprocessBudgetsWithholdPartiallyDerivedData(string kind)
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),CollisionFixture.Collision((x,y)=>[new(0,0)]));
        var limits=kind switch
        {
            "references"=>CollisionFixture.PrepareLimits with {MaxReferences=1},"bands"=>CollisionFixture.PrepareLimits with {MaxBands=1},
            _=>CollisionFixture.PrepareLimits with {MaxMetadataBytes=1}
        };
        var result=RetailCollisionMath.Prepare(raw,CollisionFixture.Cosine,limits);
        Assert.Equal(RetailCollisionStatus.DefensiveLimit,result.Status); Assert.Null(result.Data); Assert.False(result.RuntimeVerified);
    }

    [Fact]
    public void MaterialPolicyFactoryExactlyMatchesThePinned6144FileImageBytes()
    {
        var policy=RetailCollisionMaterialPolicy.FromPinnedFileImage("explicit-file-image-test");
        Assert.Equal(6144,policy.DeclaredByteLength);
        Assert.Equal(RetailCollisionMaterialPolicy.PinnedSha256,Convert.ToHexString(SHA256.HashData(policy.Bytes.Span)).ToLowerInvariant());
        Assert.Equal(RetailCollisionPolicySource.PinnedFileImageDeclaration,policy.Source); Assert.False(policy.RuntimeVerified);
    }

    private static RetailCollisionTriangle Triangle(CollisionFace face)=>new(0,face.A,face.B,face.C,face.Material,0);
}
