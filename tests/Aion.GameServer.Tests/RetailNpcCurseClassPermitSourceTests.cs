using System.Security.Cryptography;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public class RetailNpcCurseClassPermitSourceTests
{
    [SkippableFact]
    public void PinnedPermitsMatchWeaponArmorAndAccessoryIdentities()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(bonusPath),"Requires matched retail permit and bonus sources");
        byte[] bytes=File.ReadAllBytes(path!),bonusBytes=File.ReadAllBytes(bonusPath!);
        var source=RetailNpcCurseClassPermitSource.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var bonus=RetailNpcCurseItemBonusSource.Parse(bonusBytes,Convert.ToHexStringLower(SHA256.HashData(bonusBytes)));
        Assert.Equal(54153,source.ItemCount);
        var sword=new RetailNpcCurseWeaponHpSource.Candidate(100001286,"sword_n_e1_pvp_55b","SWORD","sword",15,false);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,bonus.Lookup(sword,out var swordBonus));
        Assert.True(source.TryLookup(swordBonus!,1,out byte level,out bool present));Assert.Equal(55,level);Assert.True(present);
        var neck=new RetailNpcCurseOtherEquipmentHpSource.Candidate(121001358,"necklace_authorize_test_01","NECKLACE",0,0,false);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,bonus.Lookup(neck,out var neckBonus));
        Assert.True(source.TryLookup(neckBonus!,1,out level,out present));Assert.Equal(65,level);Assert.True(present);
        Assert.False(source.TryLookup(swordBonus! with {Name="changed"},1,out _,out _));
        Assert.False(source.TryLookup(swordBonus! with {Kind="other"},1,out _,out _));
        Assert.False(source.TryLookup(swordBonus!,17,out _,out _));
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseClassPermitSource.Parse(bytes,new string('0',64)));
    }
}
