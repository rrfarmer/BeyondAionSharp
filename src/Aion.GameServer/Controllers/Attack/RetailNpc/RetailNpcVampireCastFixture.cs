using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.SkillEngine.Properties;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound integration of selected vampire casts with the
/// ordinary NPC Skill lifecycle. Area casts remain fixture-only.</summary>
internal static class RetailNpcVampireCastFixture
{
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcVampireCastFixture));
    private sealed class State(Npc npc, Player target, SkyTempleNpcProfile profile,
        RetailNpcVampirePrimaryProjection.Candidate candidate, ushort word,
        Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> plan, Func<int,int,int> draw,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance,Func<Player,Effect,bool>? preflight,bool production)
    {
        internal readonly Npc Npc=npc;
        internal readonly Player Target=target;
        internal readonly SkyTempleNpcProfile Profile=profile;
        internal readonly RetailNpcVampirePrimaryProjection.Candidate Candidate=candidate;
        internal readonly ushort Word=word;
        internal readonly Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> Plan=plan;
        internal readonly Func<int,int,int> Draw=draw;
        internal readonly Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? Resistance=resistance;
        internal readonly Func<Player,Effect,bool>? Preflight=preflight;
        internal readonly bool Production=production;
        internal int Started;
        internal readonly Dictionary<Player,Effect> Observed=new(ReferenceEqualityComparer.Instance);
        internal readonly HashSet<Effect> Rejected=new(ReferenceEqualityComparer.Instance);
    }
    private static readonly ConditionalWeakTable<Skill,State> Fixtures=new();

    internal static void AttachFromCatalogForOfflineFixture(Skill skill,
        RetailNpcVampirePrimaryProjection.Candidate candidate,Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> plan,
        Func<int,int,int> draw,Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? resistance=null,
        Func<Player,Effect,bool>? preflight=null,bool production=false)
    {
        ArgumentNullException.ThrowIfNull(skill);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(draw);
        if(skill.GetEffector() is not Npc npc ||skill.GetFirstTarget() is not Player target
            ||!npc.IsSpawned() ||npc.IsDead() ||!target.IsSpawned() ||target.IsDead()
            ||!ReferenceEquals(npc.GetWorldMapInstance(),target.GetWorldMapInstance())
            ||!RetailNpcStatCatalog.TryGet(npc,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,skill.GetSkillId(),skill.GetSkillLevel(),out var original)
            ||original.Supported ||candidate.Id!=skill.GetSkillId() ||candidate.FullSourceSha256!=original.Sha256
            ||!RetailNpcStatCatalog.TryGetSkillTiming(npc,skill.GetSkillId(),skill.GetSkillLevel(),out var timing))
            throw new InvalidOperationException("Fixture cast has no selected gated source and timing");
        lock(Fixtures)
        {
            if(Fixtures.TryGetValue(skill,out _))throw new InvalidOperationException("Fixture cast already attached");
            Fixtures.Add(skill,new(npc,target,profile,candidate,timing.Word,plan,draw,resistance,preflight,production));
        }
    }

    // Only the two byte-pinned single-target sources may enter ordinary NPC
    // casting. A complete selected player-input and timing package is required;
    // the area source retains its separate target-collection gate.
    internal static void TryAttachForNpcCombat(Skill skill)
    {
        if(skill.GetSkillId() is not (16596 or 18459) ||skill.GetEffector() is not Npc npc
            ||skill.GetFirstTarget() is not Player target ||!npc.IsSpawned() ||npc.IsDead()
            ||!target.IsSpawned() ||target.IsDead() ||!ReferenceEquals(npc.GetWorldMapInstance(),target.GetWorldMapInstance())
            ||skill.GetSkillTemplate().GetProperties() is not { } properties
            ||properties.GetFirstTarget()!=FirstTargetAttribute.TARGET ||properties.GetTargetType()!=TargetRangeAttribute.ONLYONE
            ||!RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,skill.GetSkillId(),skill.GetSkillLevel(),out var candidate)
            ||candidate.Primary.Drain is not {Area:false}
            ||!RetailNpcStatCatalog.TryGetSkillTiming(npc,skill.GetSkillId(),skill.GetSkillLevel(),out _)
            ||RetailNpcStatCatalog.VampirePlayerSources is not { } sources)return;
        lock(Fixtures)if(Fixtures.TryGetValue(skill,out _))return;
        try {AttachReviewedInputsForOfflineFixture(skill,candidate,new(sources),SkyTempleNpcCombat.Draw,true);}
        catch(InvalidOperationException ex)
        {
            Log.LogWarning("RETAIL_NPC_VAMPIRE_ADMISSION npc={Npc} skill={Skill} reason=source-or-actor-changed detail={Detail}",
                npc.GetNpcId(),skill.GetSkillId(),ex.Message);
        }
    }

    internal static RetailNpcVampirePlayerInputs AttachSelectedInputsForOfflineFixture(Skill skill,Func<int,int,int> draw)
    {
        if(skill.GetEffector() is not Npc npc
            ||!RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,skill.GetSkillId(),skill.GetSkillLevel(),out var candidate)
            ||RetailNpcStatCatalog.VampirePlayerSources is not { } sources)
            throw new InvalidOperationException("No reviewed vampire source for this NPC and skill");
        var inputs=new RetailNpcVampirePlayerInputs(sources);
        AttachReviewedInputsForOfflineFixture(skill,candidate,inputs,draw);return inputs;
    }

    internal static void AttachReviewedInputsForOfflineFixture(Skill skill,
        RetailNpcVampirePrimaryProjection.Candidate candidate,RetailNpcVampirePlayerInputs inputs,Func<int,int,int> draw,bool production=false)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if(skill.GetFirstTarget() is not Player first)throw new InvalidOperationException("Fixture requires a player first target");
        var contexts=new Dictionary<Player,RetailNpcVampirePlayerInputs>(ReferenceEqualityComparer.Instance) {[first]=inputs};
        RetailNpcVampirePlayerInputs Context(Player player)
        {
            lock(contexts)
            {
                if(!contexts.TryGetValue(player,out var selected))
                    contexts.Add(player,selected=inputs.NewTargetForOfflineFixture());
                return selected;
            }
        }
        AttachFromCatalogForOfflineFixture(skill,candidate,(player,effect)=>Context(player).Build(player,effect),
            draw,player=>Context(player).Capture(player),(player,effect)=>Context(player).CanPrepareForOfflineFixture(player,effect),production);
    }

    internal static void AttachForOfflineFixture(Skill skill, RetailNpcVampirePrimaryProjection.Candidate candidate,
        ReadOnlySpan<byte> reportBytes, string reportSha256, Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?> plan,
        Func<int,int,int> draw)
    {
        ArgumentNullException.ThrowIfNull(skill);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(draw);
        if(reportBytes.Length is <=0 or >2_000_000 || Convert.ToHexStringLower(SHA256.HashData(reportBytes))!=reportSha256)
            throw new InvalidDataException("Fixture marker report identity differs");
        if(skill.GetEffector() is not Npc npc ||skill.GetFirstTarget() is not Player target
            ||!npc.IsSpawned() ||npc.IsDead() ||!target.IsSpawned() ||target.IsDead()
            ||!ReferenceEquals(npc.GetWorldMapInstance(),target.GetWorldMapInstance())
            ||!RetailNpcStatCatalog.TryGet(npc,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,skill.GetSkillId(),skill.GetSkillLevel(),out var original)
            ||original.Supported ||candidate.Id!=skill.GetSkillId() ||candidate.FullSourceSha256!=original.Sha256)
            throw new InvalidOperationException("Fixture cast is not the selected gated vampire source");
        using var doc=JsonDocument.Parse(reportBytes.ToArray());var root=doc.RootElement;
        if(root.GetProperty("map").GetInt32()!=npc.GetWorldId() ||root.GetProperty("npc").GetInt32()!=npc.GetNpcId()
            ||root.GetProperty("skill").GetInt32()!=skill.GetSkillId()
            ||root.GetProperty("npc_source_sha256").GetString()!=profile.SourceRecordSha256
            ||root.GetProperty("skill_source_sha256").GetString()!=candidate.FullSourceSha256)
            throw new InvalidDataException("Fixture marker does not bind this cast source");
        ushort word=RetailNpcStatSkillTimingSources.ReadMarkerWord(root);
        lock(Fixtures)
        {
            if(Fixtures.TryGetValue(skill,out _))throw new InvalidOperationException("Fixture cast already attached");
            Fixtures.Add(skill,new(npc,target,profile,candidate,word,plan,draw,null,null,false));
        }
    }

    internal static bool TryPrepareForOfflineFixture(Skill skill,out int word)
    {
        word=0;if(!Fixtures.TryGetValue(skill,out var state))return false;
        if(!ReferenceEquals(skill.GetEffector(),state.Npc) ||!ReferenceEquals(skill.GetFirstTarget(),state.Target)
            ||!RetailNpcStatCatalog.TryGet(state.Npc,out var current) ||!ReferenceEquals(current,state.Profile)
            ||!state.Npc.IsSpawned() ||state.Npc.IsDead() ||!state.Target.IsSpawned() ||state.Target.IsDead()
            ||Interlocked.Exchange(ref state.Started,1)!=0)
        {
            if(!state.Production)throw new InvalidOperationException("Fixture cast source or lifetime changed");
            lock(Fixtures)Fixtures.Remove(skill);
            Log.LogWarning("RETAIL_NPC_VAMPIRE_ADMISSION npc={Npc} skill={Skill} reason=cast-source-or-lifetime-changed",state.Npc.GetNpcId(),skill.GetSkillId());
            return false;
        }
        word=state.Word;return true;
    }

    internal static void BeforeInitializationForOfflineFixture(Skill skill,Effect effect)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return;
        if(Volatile.Read(ref state.Started)!=1 ||!ReferenceEquals(effect.GetEffector(),state.Npc)
            ||effect.GetEffected() is not Player player ||!player.IsSpawned() ||player.IsDead()
            ||!ReferenceEquals(player.GetWorldMapInstance(),state.Npc.GetWorldMapInstance())
            ||!skill.GetEffectedList().Contains(player)
            ||!ReferenceEquals(player,state.Target) && state.Candidate.Primary.Drain is not {Area:true}
            ||state.Production && skill.GetEffectedList().Count!=1)
        {
            if(!state.Production)throw new InvalidOperationException("Fixture effect does not belong to the selected cast");
            lock(state.Observed)state.Rejected.Add(effect);
            effect.SetEffectResult(EffectResult.CONFLICT);
            Log.LogWarning("RETAIL_NPC_VAMPIRE_ADMISSION npc={Npc} skill={Skill} reason=effect-target-or-lifetime-changed",state.Npc.GetNpcId(),skill.GetSkillId());
            return;
        }
        lock(state.Observed)
            if(!state.Observed.TryAdd(player,effect))throw new InvalidOperationException("Duplicate fixture target effect");
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,state.Candidate,state.Plan,state.Draw,state.Resistance,state.Preflight);
    }

    internal static void AfterInitializationForOfflineFixture(Skill skill,Effect effect)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return;
        lock(state.Observed)if(state.Rejected.Contains(effect))return;
        if(effect.GetEffected() is not Player player)throw new InvalidOperationException("Fixture vampire target changed");
        lock(state.Observed)
            if(!state.Observed.TryGetValue(player,out var observed) ||!ReferenceEquals(observed,effect))
                throw new InvalidOperationException("Fixture vampire effect is not registered to target");
        if(!RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(effect,out bool handled,out bool success))
        {
            if(state.Production && effect.GetEffectResult()==EffectResult.CONFLICT)
            {
                Log.LogWarning("RETAIL_NPC_VAMPIRE_ADMISSION npc={Npc} skill={Skill} reason=effect-conflict",state.Npc.GetNpcId(),skill.GetSkillId());
                return;
            }
            throw new InvalidOperationException("Fixture vampire initialization did not run");
        }
        if(state.Production && !handled)
            Log.LogWarning("RETAIL_NPC_VAMPIRE_ADMISSION npc={Npc} skill={Skill} reason=unsupported-player-input-or-protection",state.Npc.GetNpcId(),skill.GetSkillId());
        if(handled && success)RetailNpcStatSpellDrain.AttachVampireTimelineForOfflineFixture(effect);
    }

    internal static Effect? ObservedEffectForOfflineFixture(Skill skill)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return null;
        lock(state.Observed)return state.Observed.TryGetValue(state.Target,out var effect)?effect:null;
    }

    internal static Effect? ObservedEffectForOfflineFixture(Skill skill,Player target)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return null;
        lock(state.Observed)return state.Observed.TryGetValue(target,out var effect)?effect:null;
    }

    internal static bool IsSelectedForOfflineFixture(Skill skill)
        => Fixtures.TryGetValue(skill,out var state) && Volatile.Read(ref state.Started)==1;
}
