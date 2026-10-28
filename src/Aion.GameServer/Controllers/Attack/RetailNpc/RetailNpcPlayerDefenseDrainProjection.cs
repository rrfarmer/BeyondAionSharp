using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Explicit native-stage calculations for the reviewed 17860
/// defense sibling. This does not read or change live player state, prove
/// effect admission, or calculate the player's equipment inputs.</summary>
internal static class RetailNpcPlayerDefenseDrainProjection
{
    private const string SourceHash="52953fccf5f044eb35e7ab1fdebb06af5ff5948ccd67dacecf6aacb855367ee2";

    internal static int Read17860PercentForOfflineFixture(ReadOnlySpan<byte> raw)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(raw))!=SourceHash)
            throw new InvalidDataException("Retail 17860 source hash differs");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var root=XElement.Load(reader);
        string? Field(string name)
        {
            var nodes=root.Elements(name).ToArray();
            return nodes is {Length:1} && !nodes[0].HasElements && !nodes[0].HasAttributes ? nodes[0].Value:null;
        }
        if(root.Name!="skill_base"||Field("id")!="17860"||Field("name")!="BNEL_SpellDrainMpattSDdefTA10_ch1"
            ||Field("effect1_type")!="SpellATKDrain_Instant"||Field("effect2_type")!="Statdown"
            ||Field("effect2_noresist")!="1"||Field("effect2_target_type")!="Target_Only"
            ||Field("effect2_cond_preeffect")!="e1"||Field("effect2_cond_preeffect_prob1")!="0"
            ||Field("effect2_cond_preeffect_prob2")!="100"||Field("effect2_remain2")!="60100"
            ||Field("effect2_effectid")!="10178603"||Field("effect2_reserved1")!="0"
            ||Field("effect2_reserved2")!="10"||Field("effect2_reserved6")!="1"
            ||Field("effect2_reserved13")!="PMDefend")throw new InvalidDataException("Retail 17860 defense recipe differs");
        // StatDown negates level*R1+R2. R6 selects percentage; PMDefend fans
        // it to physical defense and all six elemental defense channels.
        return -10;
    }

    // User::CalcSecondLevelAttr: the percentage uses the separately revised
    // equipped-armor basis, not the entire current physical-defense value.
    // The two native MOV EAX inputs are zero-extended dwords.
    internal static int PhysicalDefense(uint current,uint armorBasis,int aggregatePercent,int flat)
        => (int)Math.Clamp((long)current+(long)armorBasis*aggregatePercent/100+flat,0L,100000L);

    // User::CalcFirstLevelAttr copies the signed initial element word to its
    // current word, then adds the signed percentage and flat channels before
    // applying caller-supplied retail bounds. Absolute overrides are separate.
    internal static short ElementalDefense(short initial,int aggregatePercent,int flat,short minimum,short maximum)
    {
        if(minimum>maximum)throw new ArgumentException("Invalid explicit retail element bounds");
        return (short)Math.Clamp((long)initial+(long)initial*aggregatePercent/100+flat,(long)minimum,maximum);
    }
}
