using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Xunit;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicDamageStages;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;
namespace Aion.GameServer.Tests;
public class RetailNpcPeriodicDamageStagesTests
{
    private static readonly JsonElement Cases=Decode(VectorBlob,"cb4533ba216406adc38158ef3be1d8256150b9ec6a507d4f6c833013490ca330");
    private static RetailNpcNormalScope Scope(RetailNpcNormalActorKind target)=>RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,target,212772,target==RetailNpcNormalActorKind.Player?0:212774);
    public static IEnumerable<object[]> SourceDerivedVectors()=>Enumerable.Range(0,Cases.GetArrayLength()).Select(i=>new object[]{i});
    [Theory,MemberData(nameof(SourceDerivedVectors))]
    public void SourceDerivedFiniteArithmeticMatchesEveryRetainedWord(int index)
    {
        var r=Cases[index];var a=r.GetProperty("args");var e=r.GetProperty("expected");int I(int i)=>a[i].GetInt32();
        float Float(int i)=>BitConverter.UInt32BitsToSingle(a[i].GetUInt32());
        switch(r.GetProperty("op").GetString())
        {
            case "base":Assert.Equal(e.GetInt32(),M.BaseDamage((short)I(0),I(1),I(2)));break;
            case "boost":
                var b=M.SkillBoost(F.SpellAttack,I(0),I(1),a[2].GetUInt32(),I(3),I(4));
                Assert.Equal(e[0].GetInt32(),b.Difference);Assert.Equal(e[1].GetInt32(),b.CappedDifference);
                Assert.Equal(e[2].GetInt32(),b.Normalized);Assert.Equal(e[3].GetInt32(),b.CappedNormalized);
                Assert.Equal(e[4].GetUInt32(),BitConverter.SingleToUInt32Bits(b.Multiplier!.Value));
                Assert.Equal(e[5].GetUInt32(),BitConverter.SingleToUInt32Bits(b.Product!.Value));Assert.Equal(e[6].GetInt32(),b.Damage);break;
            case "bleed-boost":
                var identity=M.SkillBoost(F.Bleed,int.MinValue,int.MaxValue,0,1,I(0));Assert.Equal(e.GetInt32(),identity.Damage);
                Assert.Null(identity.Difference);Assert.Null(identity.Multiplier);Assert.Null(identity.Product);break;
            case "magic-defense":Assert.Equal(e.GetInt32(),M.MagicalDefense(F.SpellAttack,I(0),I(1)));break;
            case "bleed-defense":Assert.Equal(e.GetInt32(),M.MagicalDefense(F.Bleed,int.MinValue,I(0)));break;
            case "element":
                var target=Enum.Parse<RetailNpcNormalActorKind>(a[0].GetString()!);
                var scope=Scope(target);
                var el=M.ElementalDefense(F.SpellAttack,scope,(short)I(3),I(4),I(1),I(2));
                Assert.Equal(e[0].GetInt32(),el.LevelContribution);Assert.Equal(e[1].GetUInt32(),BitConverter.SingleToUInt32Bits(el.Denominator!.Value));
                Assert.Equal(e[2].GetUInt32(),BitConverter.SingleToUInt32Bits(el.Multiplier!.Value));Assert.Equal(e[3].GetInt32(),el.Damage);break;
            case "bleed-element":
                var bleed=M.ElementalDefense(F.Bleed,Scope(RetailNpcNormalActorKind.Player),short.MinValue,I(0),int.MinValue,int.MaxValue);
                Assert.Equal(e.GetInt32(),bleed.Damage);Assert.Null(bleed.LevelContribution);Assert.Null(bleed.Denominator);Assert.Null(bleed.Multiplier);break;
            case "critical":Assert.Equal(e.GetInt32(),M.NpcCritical(I(0),(short)I(1),Float(2),Float(3),(ushort)I(4)));break;
            case "sharing":Assert.Equal(e.GetInt32(),M.SharedDamage(I(0),(byte)I(1),I(2)));break;
            case "pack":
                var p=M.Pack(I(0),a[1].GetBoolean(),(ushort)I(2));Assert.Equal(e[0].GetUInt32(),p.Word);Assert.Equal(e[1].GetBoolean(),p.Critical);
                Assert.Equal(e[2].GetInt32(),p.DistributionCount);Assert.Equal(e[3].GetInt32(),p.BaseDamage);Assert.Equal(p,M.Unpack(p.Word));break;
            default:throw new InvalidOperationException("Unknown independently generated periodic vector");
        }
    }
    [Fact] public void UnknownProgramRejects()=>Assert.Throws<ArgumentOutOfRangeException>(()=>M.MagicalDefense((F)17,0,35));
    [Fact] public void ReachedUnrepresentableNormalizationRejects()=>Assert.Throws<ArithmeticException>(()=>M.SkillBoost(F.SpellAttack,0,0,0,0,35));
    [Fact] public void ReachedExceptionalCriticalRejects()=>Assert.Throws<ArithmeticException>(()=>M.NpcCritical(35,23,float.NaN,0,0));
    [Theory,InlineData(-1),InlineData(128)] public void UnpackedSharingDomainIsNotReinterpreted(int count)=>Assert.Throws<ArgumentOutOfRangeException>(()=>M.SharedDamage(135,1,count));
    private static JsonElement Decode(string text, string expected)
    {
        using var source = new MemoryStream(Convert.FromBase64String(text));
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream(); gzip.CopyTo(output); byte[] bytes=output.ToArray();
        Assert.Equal(expected, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var doc=JsonDocument.Parse(bytes); return doc.RootElement.Clone();
    }

    private const string VectorBlob="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";
}
