using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailMainMaterialStatus { CalculatedMaterial, IncompleteAtSelector, IncompleteAtRegionFallback, IncompleteAtActiveMaterial, InvalidStorage, UnsupportedConsumedValue, BudgetExceeded }
public sealed record RetailMainMaterialResult(RetailMainMaterialStatus Status, byte? Material, int? Selector,
    ImmutableArray<string> EvaluatedSkips, ulong? IncompleteChild, string? Detail)
{
    public bool Computed => Status==RetailMainMaterialStatus.CalculatedMaterial;
    public bool NativeConstructorVerified => false;
    public bool ActivationReady => false;
}

/// <summary>Inactive Main1400ad660/ce58e0/ce6b00, distinct from bilinear terrain or collision queries.</summary>
public static class RetailNpcMainMaterialQuery
{
    internal static void ValidateStorage(RetailMainOwnedWorld world,RetailMainApplyLimits limits)
    {
        var m=world.Material;RetailMainApplyEvidence.Source(m.Source);
        RetailMainApplyEvidence.Require(m.WidthMetres==world.WidthMetres&&m.HeightMetres==world.HeightMetres&&
            m.Stride1c8==(world.WidthMetres/2)+1,"Declared HeightMap40 mapping dimensions/stride disagree.");
        long samples=(long)m.Stride1c8*((world.HeightMetres/2)+1);
        RetailMainApplyEvidence.Require(samples*2==m.HeightWords.Length&&samples==m.Plane1b0.Length&&
            (m.Mode1e0!=0||samples==m.LegacyTypes1b8.Length)&&m.MaterialRecords.Length==256*0x44,
            "Complete declared sample planes and256x68 material table required.");
        long bytes=(long)m.HeightWords.Length+m.Plane1b0.Length+m.LegacyTypes1b8.Length+m.MaterialRecords.Length;
        int columns=world.RcuStride5c,rows=(world.HeightMetres+63)/64;
        RetailMainApplyEvidence.Require((long)columns*rows==m.CellBlocks.Length,"Complete64m sector-cell allocation map required.");
        var regions=new List<(ulong Begin,ulong End)>();
        foreach(var block in m.CellBlocks)
        {
            bytes+=block.Bytes.Length;
            RetailMainApplyEvidence.Require(block.Address!=0&&block.Bytes.Length==32*32*16&&block.Address<=ulong.MaxValue-(uint)block.Bytes.Length,
                "Each owned material sector needs a nonzero16KiB allocation.");
            regions.Add((block.Address,block.Address+(uint)block.Bytes.Length));
        }
        RetailMainApplyEvidence.Require(bytes<=limits.MaxMaterialBytes,"Owned material storage exceeds defensive budget.",RetailMainApplyStatus.BudgetExceeded);
        regions.Sort((a,b)=>a.Begin.CompareTo(b.Begin));
        for(int i=1;i<regions.Count;i++)RetailMainApplyEvidence.Require(regions[i-1].End<=regions[i].Begin,"Material sector allocations overlap.");
    }

    public static RetailMainMaterialResult Query(RetailMainOwnedWorld world,RetailMainVectorWords position,RetailMainApplyLimits? limits=null)
    {
        ArgumentNullException.ThrowIfNull(world);limits??=new();
        try
        {
            RetailNpcMainWorldMotion.ValidateDimensions(world,limits);ValidateStorage(world,limits);
            var (value,selector)=Calculate(world,position);
            return new(RetailMainMaterialStatus.CalculatedMaterial,value,selector,[],null,null);
        }
        catch(RetailMainApplyFault f)
        {
            return new(f.Child==0x1400ad7af?RetailMainMaterialStatus.IncompleteAtSelector:
                f.Status==RetailMainApplyStatus.BudgetExceeded?RetailMainMaterialStatus.BudgetExceeded:
                f.Status==RetailMainApplyStatus.UnsupportedConsumedValue?RetailMainMaterialStatus.UnsupportedConsumedValue:
                RetailMainMaterialStatus.InvalidStorage,null,null,[],f.Child,f.Message);
        }
    }

    internal static (byte Material,int? Selector) Calculate(RetailMainOwnedWorld world,RetailMainVectorWords position)
    {
        if(!RetailNpcMainWorldMotion.InBounds(world,position))return (0,null);
        int x=RetailMainApplyMath.Truncate32(position.X),y=RetailMainApplyMath.Truncate32(position.Y);
        var m=world.Material;int halfX=x>>1,halfY=y>>1;
        int sample=checked(m.Stride1c8*halfY+halfX);
        RetailMainApplyEvidence.Require((uint)sample<(uint)m.Plane1b0.Length,"Material sample outside owned storage.");
        ushort word=BinaryPrimitives.ReadUInt16LittleEndian(m.HeightWords.AsSpan(sample*2,2));
        byte plane=m.Plane1b0[sample];int selector=m.Mode1e0==0?m.LegacyTypes1b8[sample]:((word&3)<<2)|(plane>>6);
        int sector=checked((y>>6)*world.RcuStride5c+(x>>6)),cell=(((halfY&31)*32)+(halfX&31))*16;
        RetailMainApplyEvidence.Require((uint)sector<(uint)m.CellBlocks.Length&&cell>=0&&cell+16<=m.CellBlocks[sector].Bytes.Length,
            "Native material cell pointer outside owned allocation.");
        // The native ADD/JZ checks the formed pointer, not just the sector base.
        ulong pointer=m.CellBlocks[sector].Address+(uint)cell;
        if(pointer==0)return (0,selector);
        if(selector==12)return (0,selector); // Complete3-byte checked body. Region fallback remains a separate child.
        if(selector is not (0 or 8))RetailMainApplyEvidence.Child(0x1400ad7af,$"Material selector{selector} requires its actual dispatcher target.");
        byte material=m.Mode1e0==0?plane:(byte)(plane&63);
        uint height=RetailMainApplyMath.Mul(RetailMainApplyMath.Integer((uint)(word&0xfffc)),0x3d000000);
        float delta=RetailMainApplyMath.Float(RetailMainApplyMath.Sub(position.Z,height));
        return (delta<2f&&delta>-2f||material==10&&delta<0?material:(byte)0,selector);
    }

    /// <summary>Complete admitted four-entry skip evaluation; no desired callback return or skill application input.</summary>
    public static RetailMainMaterialResult EvaluateEffects(RetailMainOwnedWorld world,RetailNpcMainActorSnapshot actor,byte computedMaterial)
    {
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(actor);
        try { RetailMainApplyEvidence.Source(world.Source);RetailMainApplyEvidence.Source(world.Material.Source);RetailMainApplyEvidence.Source(actor.Source); }
        catch(RetailMainApplyFault f){return new(RetailMainMaterialStatus.InvalidStorage,null,null,[],null,f.Message);}
        List<string> skips=[];
        if(computedMaterial==0)return new(RetailMainMaterialStatus.IncompleteAtRegionFallback,0,null,[],0x140d76e00,"Material0 requires region query0x2000; zero is not proof of no effects.");
        int record=computedMaterial*0x44;
        if(world.Material.MaterialRecords.Length!=256*0x44)return new(RetailMainMaterialStatus.InvalidStorage,null,null,[],null,"Complete material table required.");
        for(int i=0;i<4;i++)
        {
            ReadOnlySpan<byte> bytes=world.Material.MaterialRecords.AsSpan();
            uint skill=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(record+4+i*4)..]);
            ushort level=BinaryPrimitives.ReadUInt16LittleEndian(bytes[(record+0x14+i*2)..]);
            uint interval=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(record+0x1c+i*4)..]);
            uint targets=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(record+0x2c+i*4)..]);
            string? reason=skill==0?"skillID0":level==0?"level0":interval==0?"interval0":
                actor.NativeClass144==1&&targets==1?"class1/target1":
                actor.NativeClass144==2&&(actor.Owner188c<=0)&&(targets&0xfffffffd)==0?"ownerlessNPC/targetmask":null;
            if(reason is null)return new(RetailMainMaterialStatus.IncompleteAtActiveMaterial,computedMaterial,null,skips.ToImmutableArray(),
                0x14037c1fd,$"Material entry{i} reaches clock/conditions/skill application; no poststate input is admitted.");
            skips.Add($"material[{i}]:{reason}");
        }
        return new(RetailMainMaterialStatus.CalculatedMaterial,computedMaterial,null,skips.ToImmutableArray(),null,null);
    }
}
