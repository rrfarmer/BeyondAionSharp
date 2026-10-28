using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline projection of the primary operands of the three selected
/// vampire skills. A projected primary is not a complete skill and cannot be
/// published by the gameplay catalog.</summary>
internal static class RetailNpcVampirePrimaryProjection
{
    private static readonly Dictionary<int,string> Selected = new()
    {
        [16596]="05fb066d3345c3c881a852ab89af252994a2b779752f70aeb935e92fc5b862a5",
        [18459]="46416d8ce4bb33386248b2ac7e2601408a2da11efa5a3cca2d8f2e9ed6ca9e49",
        [18460]="b9f134dc21f88bed08db52512c62ad4415a8965136ce6cf074e2b9f906d08a98"
    };

    internal static bool IsReviewedSource(int id)=>Selected.ContainsKey(id);

    internal sealed record Candidate(int Id,string FullSourceSha256,RetailNpcStatSkillCombat.Source Primary);

    private sealed class OfflineAdmission(Candidate candidate,RetailNpcCurseResourceOwner.Plan? plan,Func<int,int,int> draw,
        Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?>? factory,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance,Func<Player,Effect,bool>? preflight)
    {
        internal readonly Candidate Candidate=candidate;
        internal readonly RetailNpcCurseResourceOwner.Plan? Plan=plan;
        internal readonly Func<int,int,int> Draw=draw;
        internal readonly Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?>? Factory=factory;
        internal readonly Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? Resistance=resistance;
        internal readonly Func<Player,Effect,bool>? Preflight=preflight;
        internal bool Attempted,Handled,Success;
    }
    private static readonly ConditionalWeakTable<Effect,OfflineAdmission> OfflineAdmissions=new();

    // Per-effect admission joins the primary with a complete resource plan.
    // Tests may supply deterministic inputs; selected live casts use current
    // reviewed player inputs and the normal NPC random source.
    internal static void AttachOfflineInitialization(Effect effect,Candidate candidate,
        RetailNpcCurseResourceOwner.Plan plan,Func<int,int,int> draw,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance=null)
    {
        AttachOfflineInitialization(effect,candidate,plan,draw,null,resistance,null);
    }

    internal static void AttachOfflineInitialization(Effect effect,Candidate candidate,
        Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> factory,Func<int,int,int> draw,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance=null,Func<Player,Effect,bool>? preflight=null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        AttachOfflineInitialization(effect,candidate,null,draw,factory,resistance,preflight);
    }

    private static void AttachOfflineInitialization(Effect effect,Candidate candidate,
        RetailNpcCurseResourceOwner.Plan? plan,Func<int,int,int> draw,
        Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?>? factory,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance,Func<Player,Effect,bool>? preflight)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(draw);
        if((plan is null)==(factory is null))throw new ArgumentException("Supply exactly one offline resource plan or factory");
        if(effect.GetEffector() is not Npc ||effect.GetEffected() is not Player
            ||effect.GetEffectTemplates().FirstOrDefault() is not SpellAtkDrainInstantEffect primary
            ||primary.Position!=1 ||effect.RetailHasEnded)
            throw new InvalidOperationException("Offline vampire fixture requires the selected NPC primary");
        OfflineAdmissions.Add(effect,new(candidate,plan,draw,factory,resistance,preflight));
    }

    internal static bool TryConsumeOfflineInitialization(Effect effect,EffectTemplate primary,out bool success)
    {
        success=false;
        if(!OfflineAdmissions.TryGetValue(effect,out var admission))return false;
        if(primary.GetType()!=typeof(SpellAtkDrainInstantEffect) ||primary.Position!=1
            ||!ReferenceEquals(effect.GetEffectTemplates().FirstOrDefault(),primary))
            throw new InvalidOperationException("Offline vampire primary changed");
        lock(admission)
        {
            if(admission.Attempted)throw new InvalidOperationException("Offline vampire initialization cannot repeat");
            admission.Attempted=true;
            admission.Handled=RetailNpcStatSkillCombat.TryPrepareVampireForOfflineFixture(effect,
                admission.Candidate,admission.Plan,admission.Draw,out bool applied,admission.Factory,admission.Resistance,admission.Preflight);
            admission.Success=admission.Handled && applied;success=admission.Success;
            return true; // Declined opt-in fixtures never silently fall back.
        }
    }

    internal static bool TryGetOfflineInitialization(Effect effect,out bool handled,out bool success)
    {
        handled=false;success=false;
        if(!OfflineAdmissions.TryGetValue(effect,out var admission))return false;
        lock(admission){handled=admission.Handled;success=admission.Success;return admission.Attempted;}
    }

    internal static Candidate ReadForOfflineFixture(int id,ReadOnlySpan<byte> completeSource)
    {
        if(!Selected.TryGetValue(id,out string? expected) || completeSource.Length is <=0 or >131072
            ||Convert.ToHexStringLower(SHA256.HashData(completeSource))!=expected)
            throw new InvalidDataException("Unreviewed vampire source identity");
        string xml=new UnicodeEncoding(false,false,true).GetString(completeSource);
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings
        {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
        var root=XDocument.Load(reader,LoadOptions.PreserveWhitespace).Root;
        if(root?.Name!="skill_base" ||root.Elements("id").SingleOrDefault()?.Value!=id.ToString()
            ||root.Elements("effect2_type").SingleOrDefault()?.Value!="curse")
            throw new InvalidDataException("Vampire source identity or curse shape differs");
        // Only a byte-pinned complete source may be projected. Removing the
        // curse fields here is solely for the existing primary parser; it does
        // not declare the original two-effect source supported in gameplay.
        root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal)).Remove();
        byte[] primaryBytes=Encoding.Unicode.GetBytes(root.ToString(SaveOptions.DisableFormatting));
        string primarySha=Convert.ToHexStringLower(SHA256.HashData(primaryBytes));
        var primary=RetailNpcStatSkillCombat.ParseSource(primaryBytes,primarySha);
        if(!primary.Supported ||primary.Id!=id ||primary.Family!=RetailNpcStatSkillCombat.Kind.SpellDrain
            ||primary.Drain is null ||primary.Drain.Curse is not null)
            throw new InvalidDataException("Selected vampire primary projection is unsupported");
        return new(id,expected,primary);
    }
}
