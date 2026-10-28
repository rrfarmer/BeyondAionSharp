using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalInputs;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserNormalRecordCalculation;
using F = Aion.GameServer.Tests.NormalRecordFixture;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcUserNormalInputsTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MDefPrimaryBackupAndLiveRetainUnsignedDwords(int selector)
    {
        var f=new F();f.W32(F.User+0x1498,0x80000001);if(selector<2){ulong p=selector==0?F.Primary:F.Backup;f.W16(p,1);f.W32(p+0x214,2);f.W32(p+0x1d0,0xffffffff);}
        var result=N.Defense(f.Cursor(),F.User,false);Assert.Equal(selector,result.Selector);Assert.Equal(selector<2?0xffffffffu:0x80000001u,result.Bits);
    }
    [Fact]
    public void OccupiedPrimaryWithoutMDefFlagSkipsBackup()
    {var f=new F();f.W16(F.Primary,1);f.W16(F.Backup,1);f.W32(F.Backup+0x214,2);f.W32(F.Backup+0x1d0,999);f.Forget(F.Backup+0x1d0,4);var c=f.Cursor();Assert.Equal(200u,N.Defense(c,F.User,false).Bits);Assert.DoesNotContain(c.Steps,x=>x.Address==F.Backup+0x1d0);}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryElementUsesItsSignedWordAndCorrespondingFlag(int index)
    {var f=new F();f.W16(F.Primary,1);f.W16(F.Primary+0xbc,(ushort)(1<<(index+6)));f.W16(F.Primary+0xb0+(ulong)index*2,0x8000);var r=N.Elemental(f.Cursor(),F.User,index);Assert.Equal(short.MinValue,r.Value);Assert.Equal(0,r.Selector);}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ElementalGetterPreservesBackupAndLiveChoice(int selector)
    {var f=new F();if(selector<2){ulong p=selector==0?F.Primary:F.Backup;f.W16(p,1);f.W16(p+0xbc,0x40);f.W16(p+0xb0,0xffff);}var r=N.Elemental(f.Cursor(),F.User,0);Assert.Equal(selector,r.Selector);Assert.Equal(selector<2?(short)-1:(short)100,r.Value);}
    [Fact]
    public void OccupiedPrimaryElementWithoutFlagGoesDirectlyToLive()
    {var f=new F();f.W16(F.Primary,1);f.Forget(F.Backup,0x218);Assert.Equal((short)100,N.Elemental(f.Cursor(),F.User,0).Value);}
    [Theory]
    [InlineData(-1)] [InlineData(6)] [InlineData(32)]
    public void InvalidElementIndexDoesNotShiftOrReadAnAdjacentField(int index)
    {Assert.Throws<U.Domain>(()=>N.Elemental(new F().Cursor(),F.User,index));}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MResFullSnapshotWidthAndNpcNonPvpLiveBranch(int selector)
    {var f=new F();f.W16(F.User+0x149c,65535);if(selector<2){ulong p=selector==0?F.Primary:F.Backup;f.W16(p,1);f.W32(p+0x214,4);f.W32(p+0x1d4,0x80000001);}f.Forget(F.User+0xdcc,4);f.Forget(F.User+0xd9c,4);var r=N.MagicalResistance(f.Cursor(),F.User);Assert.Equal(selector,r.Selector);Assert.Equal(selector<2?0x80000001u:65535u,r.Bits);}
    [Theory]
    [InlineData(false,false,0)] [InlineData(false,false,1)] [InlineData(false,true,0)] [InlineData(false,true,1)]
    [InlineData(true,false,0)] [InlineData(true,false,1)] [InlineData(true,true,0)] [InlineData(true,true,1)]
    public void CriticalSnapshotValuesStaySigned32(bool physical,bool reduction,int selector)
    {
        var f=new F();ulong p=selector==0?F.Primary:F.Backup;f.W16(p,1);uint bit=physical?(reduction?0x1000u:0x800u):(reduction?0x800u:0x400u);
        ulong flag=physical?0x1c8ul:0x214ul,field=physical?(reduction?0x1a4ul:0x1a0ul):(reduction?0x1f8ul:0x1f4ul);
        f.W32(p+flag,bit);f.W32(p+field,0x80000001);Assert.Equal(unchecked((int)0x80000001),N.Critical(f.Cursor(),F.User,physical,reduction));
    }
    [Fact]
    public void SharedPrimaryBackupAliasesUseOnePrefixAndTailLedger()
    {var f=new F();f.W64(F.User+0x6e38,F.Primary);var c=f.Cursor();Assert.Equal(200u,N.Defense(c,F.User,false).Bits);Assert.Equal(0u,N.MagicalResistance(c,F.User).Bits);Assert.Equal(0,N.Critical(c,F.User,false,true));Assert.Equal((ushort)500,U.Status(c,F.User,U.StatusGetter.Dodge).Value);}
    [Fact]
    public void UnknownTailFlagsFailInsteadOfReadingAZeroDefault()
    {var f=new F();f.W16(F.Primary,1);f.Forget(F.Primary+0x214,4);Assert.Throws<U.Domain>(()=>N.MagicalResistance(f.Cursor(),F.User));}
    [Theory]
    [InlineData(0,-1000)] [InlineData(50,-1000)] [InlineData(51,-1010)] [InlineData(80,-1300)] [InlineData(100,-1500)]
    public void CurrentFloorUsesTheBoundUserLevelCell(int level,int expected)
    {var f=new F();f.W16(F.User+0x1492,(ushort)level);Assert.Equal(expected,N.Floor(f.Cursor(),F.User));}
    [Fact]
    public void UnknownCurrentFloorOrBeyondInitializerLevelCannotUseANeighbor()
    {var f=new F();f.Forget(0x145a28930+30*4,4);Assert.Throws<U.Domain>(()=>N.Floor(f.Cursor(),F.User));f.W16(F.User+0x1492,101);Assert.Throws<U.Domain>(()=>N.Floor(f.Cursor(),F.User));}
    [Theory]
    [InlineData(50,80,0)] [InlineData(80,50,0)] [InlineData(80,75,250)] [InlineData(60,80,100)]
    public void DenominatorReadsOriginalRepeatedLevelGetters(int source,int target,int expected)
    {var f=new F();f.W16(F.Npc+0x1492,(ushort)source);f.W16(F.User+0x1492,(ushort)target);var c=f.Cursor();Assert.Equal(expected,N.DenominatorAdjustment(c,F.Npc,F.User));Assert.Equal(source<=50?1:target<=50?2:5,c.Steps.Count(x=>x.Operand=="actor.level1492"));}
    [Fact]
    public void MResUnsignedSubtractionIsConsumedWithSignedWrapInTheNormalParent()
    {var f=new F("magical_group_no_resist");f.W16(F.Primary,1);f.W32(F.Primary+0x214,4);f.W32(F.Primary+0x1d4,uint.MaxValue);var o=C.CalculateFirstRecord(f.Input());Assert.True(o.Success,o.Failure?.ToString());Assert.Equal(0L,Assert.Single(o.Value!.Steps,x=>x.Operand=="resist.threshold").Value);}
    [Fact]
    public void UnrepresentableUnsignedMDefConversionWithholdsTheWholeRecord()
    {var f=new F("scalar_element_air");f.W32(F.User+0x1498,0x80000000);var o=C.CalculateFirstRecord(f.Input());Assert.False(o.Success);Assert.Equal("conversion",o.Failure!.Code);Assert.Null(o.Value);}
}
