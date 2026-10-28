using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline projection of the selected 17860 primary only. The
/// StatDown sibling, actual actor state, damage owner and target selection
/// remain mandatory for a complete skill and are not enabled here.</summary>
internal static class RetailNpc17860PrimaryProjection
{
    private const string SourceSha="52953fccf5f044eb35e7ab1fdebb06af5ff5948ccd67dacecf6aacb855367ee2";
    private sealed class OfflineInitialization(Candidate candidate,Func<int,int,int> draw)
    {
        internal bool Attempted,Handled,Success;
        internal readonly Candidate Candidate=candidate;
        internal readonly Func<int,int,int> Draw=draw;
    }
    private static readonly ConditionalWeakTable<Effect,OfflineInitialization> Initializations=new();

    internal static void AttachInitializationForOfflineFixture(Effect effect,Candidate candidate,Func<int,int,int> draw)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(candidate);ArgumentNullException.ThrowIfNull(draw);
        if(effect.RetailHasEnded ||effect.GetSkillId()!=17860 ||effect.GetEffector() is not Npc ||effect.GetEffected() is not Player
            ||effect.GetEffectTemplates() is not {Count:2} templates ||templates[0] is not SpellAtkDrainInstantEffect
            ||templates[1] is not StatdownEffect ||effect.GetSuccessEffects().Count!=0)
            throw new InvalidOperationException("Unreviewed 17860 initialization fixture");
        Initializations.Add(effect,new(candidate,draw));
    }

    internal static bool TryConsumeInitializationForOfflineFixture(Effect effect,EffectTemplate primary,out bool success)
    {
        success=false;
        if(!Initializations.TryGetValue(effect,out var state))return false;
        if(primary.GetType()!=typeof(SpellAtkDrainInstantEffect) ||primary.Position!=1
            ||!ReferenceEquals(effect.GetEffectTemplates().FirstOrDefault(),primary))
            throw new InvalidOperationException("Prepared 17860 primary changed");
        lock(state)
        {
            if(state.Attempted)throw new InvalidOperationException("Prepared 17860 initialization cannot repeat");
            state.Attempted=true;
            state.Handled=RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(effect,state.Candidate,state.Draw,out bool applied);
            state.Success=state.Handled &&applied;success=state.Success;
            return true; // A declined fixture must not silently use compatibility damage.
        }
    }

    internal static bool TryGetInitializationForOfflineFixture(Effect effect,out bool handled,out bool success)
    {
        handled=false;success=false;
        if(!Initializations.TryGetValue(effect,out var state))return false;
        lock(state){handled=state.Handled;success=state.Success;return state.Attempted;}
    }
    internal sealed record Candidate(RetailNpcStatSkillCombat.Source Primary)
    {
        internal bool GameplayReady=>false;
        internal RetailNpcStatSpellBonus.Result ApplyConditionalBonus(int baseDamage,int skillLevel,PlayerClass targetClass,
            Race targetRace,int targetStatusMask,int randomType,int randomMask,Func<int,int,int> draw)
        {
            if(Primary.SpellBonus is not { } bonus)throw new InvalidOperationException("Missing selected conditional damage");
            return RetailNpcStatSpellBonus.Apply(baseDamage,bonus.ValueAtLevel(skillLevel),bonus.Percent,
                bonus.AppliesTo(targetClass,targetRace,targetStatusMask),randomType,randomMask,draw);
        }
        // This is the protected/reserved hit argument supplied by the caller,
        // not a claim about actual HP lost or a scheduled caster heal.
        internal int HealFromProtectedHit(int protectedDamage)
            =>RetailNpcStatSpellDrain.HealAmount(protectedDamage,Primary.Drain!.HpPercent);
    }

    internal static Candidate ReadForOfflineFixture(ReadOnlySpan<byte> source)
    {
        if(source.Length is <=0 or >131072||Convert.ToHexStringLower(SHA256.HashData(source))!=SourceSha)
            throw new InvalidDataException("Unreviewed 17860 source identity");
        // Verify the sibling before constructing a primary-only fixture; this
        // never treats that projected record as the complete retail skill.
        _=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source);
        using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(source)),
            new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
        var root=XDocument.Load(reader,LoadOptions.PreserveWhitespace).Root;
        if(root?.Name!="skill_base"||root.Elements("id").SingleOrDefault()?.Value!="17860")
            throw new InvalidDataException("Invalid 17860 source");
        root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal)).Remove();
        byte[] primary=Encoding.Unicode.GetBytes(root.ToString(SaveOptions.DisableFormatting));
        var selected=RetailNpcStatSkillCombat.ParseSource(primary,Convert.ToHexStringLower(SHA256.HashData(primary)));
        if(!selected.Supported||selected.Id!=17860||selected.Family!=RetailNpcStatSkillCombat.Kind.SpellDrain
            ||selected.Drain is not {HpPercent:200}||selected.SpellBonus is not {Rate:0,Constant:250,Percent:true,StatusMask:RetailNpcStatTargetConditions.ParalyzeMask})
            throw new InvalidDataException("Unreviewed 17860 primary projection");
        return new(selected);
    }
}
