using System.Security.Cryptography;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-checked player shield data available to NPC drain combat.
/// Unknown records and levels do not imply a compatible protection owner.</summary>
internal sealed record RetailNpcPlayerShieldSource(int Id,int Level,int PerHit,int InitialPool,int Chance)
{
    private static readonly FrozenDictionary<int,string> StoneSkinHashes=new Dictionary<int,string>
    {
        [1155]="301d15513485ac4ef0826fee37a18b6fc1f86458aca010844efdb47140f82cf6",
        [1156]="5151a1b280f85970f2fe39939c8ba857c87f4cb0e1c23037604fee9e2f843518",
        [1157]="069f1d0d8fd681cb3fefb21eb3e7d0b7631ea17c2aab950cc1f09f4c4d5145fb",
        [1158]="51e76461fde5c61dd640fa0e72db660720f4f6860e3cafe4a1f74d3585b943a8",
        [1159]="f91367b16126a66bf83596e8c963f701c320520c0284d605b70231c7bcae8c3e",
        [1160]="19f4d877b363596e8ac4b87a135fe17da8346158ede220748fc97906789f5b7e",
        [1161]="37ec5667e598f9a7a3b13c80183fa4c7fb7148a9172c1d9e23027dbbfd9d326b",
        [1162]="90a2ac5b4b72d12f4bb0b640c531e8e4d355f9a1b1c22cefcb5357105a93011a",
        [1163]="7c3b6acfcab39ae0356cf0ced9ca40a5ff40f6b92935d2649768941c8ec4db77",
        [1164]="1723f545d78fdd20c63b558d3f0bef805d4597c42a0f680ee1cd9c95b88d35da",
        [1165]="724c891e2019445e316b24fd6af705e7e2f7e6d6fe345380886560cb99a2eb33",
        [1166]="82fc824c406c80eeb2d534622201b2c6cbf247eae9af9fdd524372ba0df292d7"
    }.ToFrozenDictionary();

    internal static RetailNpcPlayerShieldSource Parse(ReadOnlySpan<byte> bytes,string expectedSha)
    {
        if(bytes.Length is <=0 or >131072 ||Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)
            throw new InvalidDataException("Unreviewed retail player shield source");
        string xml=new UnicodeEncoding(false,false,true).GetString(bytes);
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings
            {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
        var root=XDocument.Load(reader).Root;
        if(root?.Name!="skill_base")throw new InvalidDataException("Invalid retail player shield root");
        string? Field(string name)
        {
            var nodes=root.Elements(name).ToArray();
            if(nodes.Length>1 || nodes.Any(x=>x.HasElements))throw new InvalidDataException("Ambiguous retail shield field: "+name);
            return nodes.SingleOrDefault()?.Value;
        }
        if(!int.TryParse(Field("id"),NumberStyles.None,CultureInfo.InvariantCulture,out int id)
            ||!StoneSkinHashes.TryGetValue(id,out var expected) ||expected!=expectedSha
            ||Field("name")!=$"MA_StoneSkin_G{id-1154}" ||Field("first_target")!="Me"
            ||Field("target_range")!="OnlyOne" ||Field("effect1_type")!="Shield"
            ||Field("effect1_target_type")!="Target_Only" ||Field("effect1_reserved6") is not null
            ||Field("effect1_reserved_cond1")!="EveryHit"
            ||Field("effect1_reserved_cond1_prob1")!="0" ||Field("effect1_reserved_cond1_prob2")!="1000")
            throw new InvalidDataException("Unreviewed retail shield semantics");
        int Read(string name)=>int.TryParse(Field(name),NumberStyles.None,CultureInfo.InvariantCulture,out int value)
            ?value:throw new InvalidDataException("Invalid shield scalar: "+name);
        int perHit=checked(Read("effect1_reserved1")+Read("effect1_reserved2"));
        int pool=checked(Read("effect1_reserved7")+Read("effect1_reserved8"));
        if(perHit<=0||pool<=0)throw new InvalidDataException("Invalid shield capacity");
        return new(id,1,perHit,pool,1000);
    }
}

internal sealed class RetailNpcPlayerShieldCatalog
{
    private readonly FrozenDictionary<int,RetailNpcPlayerShieldSource> records;
    internal RetailNpcPlayerShieldCatalog(IEnumerable<RetailNpcPlayerShieldSource> source)
    {
        var entries=source.ToArray();
        if(entries.Length is <1 or >12 ||entries.Select(x=>x.Id).Distinct().Count()!=entries.Length)
            throw new InvalidDataException("Invalid retail player shield catalog");
        records=entries.ToFrozenDictionary(x=>x.Id);
    }
    internal RetailNpcPlayerShieldSource? Find(int id)=>records.GetValueOrDefault(id);
}
