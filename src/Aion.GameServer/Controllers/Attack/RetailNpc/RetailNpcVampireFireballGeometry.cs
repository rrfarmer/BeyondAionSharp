using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Geometry-only projection for the pinned 5.8 vampire area skill.
/// World collection, target restrictions and visibility remain separate gates;
/// a result here never authorizes a gameplay cast.</summary>
internal sealed class RetailNpcVampireFireballGeometry
{
    private const string SourceSha="b9f134dc21f88bed08db52512c62ad4415a8965136ce6cf074e2b9f906d08a98";
    internal enum Result { EligibleForFurtherChecks, OutsideGeometry, Unrepresentable }
    internal enum EnemyRestrictionResult { EligibleForFurtherChecks, Rejected, Unresolved }
    internal readonly record struct Point(float X,float Y,float Z);
    internal readonly record struct Bounds(float Front,float Side);
    private readonly float radius,height;
    private RetailNpcVampireFireballGeometry(float radius,float height){this.radius=radius;this.height=height;}

    internal static RetailNpcVampireFireballGeometry ReadReviewedSource(ReadOnlySpan<byte> raw)
    {
        if(raw.Length is <=0 or >131072 ||Convert.ToHexStringLower(SHA256.HashData(raw))!=SourceSha)
            throw new InvalidDataException("Unreviewed vampire fireball source");
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
        var root=XDocument.Load(reader).Root;
        if(root?.Name!="skill_base")throw new InvalidDataException("Invalid vampire skill root");
        string? Read(string key)=>root.Elements(key).SingleOrDefault()?.Value;
        if(Read("id")!="18460"||Read("target_maxcount")!="8"||Read("first_target")!="Target"
            ||Read("target_range")!="Area"||!string.Equals(Read("target_range_area_type"),"Fireball",StringComparison.OrdinalIgnoreCase)
            ||Read("target_relation_restriction")!="Enemy"||Read("target_species_restriction")!="All"
            ||Read("target_range_opt2") is not null
            ||!float.TryParse(Read("target_range_opt1"),NumberStyles.Float,CultureInfo.InvariantCulture,out float r)
            ||!float.TryParse(Read("target_range_opt3"),NumberStyles.Float,CultureInfo.InvariantCulture,out float h)
            ||r!=7f||h!=7f)throw new InvalidDataException("Unreviewed vampire fireball operands");
        return new(r,h);
    }

    // Only the early, ownerless-NPC branch of the retail enemy callback is
    // established here. The relation must be the actual 5.8 matrix entry for
    // the two actors; absent entries and every other branch remain unresolved.
    // Passing this check does not establish the other target restrictions.
    internal EnemyRestrictionResult EvaluateOwnerlessNpcEnemyRestriction(
        int casterObjectId,int targetObjectId,int casterNativeType,int casterOwnerId,int? retailMatrixEntry)
    {
        if(casterObjectId==targetObjectId)return EnemyRestrictionResult.Rejected;
        if(casterNativeType==2&&casterOwnerId==0&&retailMatrixEntry is 0 or 1)
            return EnemyRestrictionResult.EligibleForFurtherChecks;
        return EnemyRestrictionResult.Unresolved;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a,float b)=>a*b;

    // The native collector checks the first target first, then traverses its
    // world-grid candidates in their supplied order. The caller must supply
    // that order and the complete native restriction/visibility decision;
    // this method cannot infer either from a 4.8 known list.
    internal IReadOnlyList<T> SelectFromReviewedOrder<T>(T firstTarget,IEnumerable<T> worldOrder,
        Func<T,bool> completeRestriction) where T:class
    {
        ArgumentNullException.ThrowIfNull(firstTarget);ArgumentNullException.ThrowIfNull(worldOrder);
        ArgumentNullException.ThrowIfNull(completeRestriction);
        var result=new List<T>(8);
        if(completeRestriction(firstTarget))result.Add(firstTarget);
        foreach(var candidate in worldOrder)
        {
            if(candidate is null)throw new ArgumentException("World candidate is null",nameof(worldOrder));
            if(ReferenceEquals(candidate,firstTarget))continue;
            bool eligible=completeRestriction(candidate);
            if(eligible&&result.Count<8)result.Add(candidate);
        }
        return result;
    }

    internal Result Evaluate(Point firstTarget,Bounds caster,Point candidate,Bounds candidateBounds)
    {
        if(!float.IsFinite(firstTarget.X)||!float.IsFinite(firstTarget.Y)||!float.IsFinite(firstTarget.Z)
            ||!float.IsFinite(candidate.X)||!float.IsFinite(candidate.Y)||!float.IsFinite(candidate.Z)
            ||!float.IsFinite(caster.Front)||!float.IsFinite(caster.Side)||caster.Front<0||caster.Side<0
            ||!float.IsFinite(candidateBounds.Front)||!float.IsFinite(candidateBounds.Side)
            ||candidateBounds.Front<0||candidateBounds.Side<0)return Result.Unrepresentable;
        float low=Sub(firstTarget.Z,height),high=Add(firstTarget.Z,height);
        float expanded=Add(Add(Mul(Math.Max(caster.Front,caster.Side),0.5f),radius),
            Mul(Math.Max(candidateBounds.Front,candidateBounds.Side),0.5f));
        float dx=Sub(candidate.X,firstTarget.X),dy=Sub(candidate.Y,firstTarget.Y);
        float squared=Add(Mul(dy,dy),Mul(dx,dx)),limit=Mul(expanded,expanded);
        if(!float.IsFinite(low)||!float.IsFinite(high)||!float.IsFinite(squared)||!float.IsFinite(limit))return Result.Unrepresentable;
        return low<=candidate.Z&&candidate.Z<=high&&squared<limit
            ?Result.EligibleForFurtherChecks:Result.OutsideGeometry;
    }
}
