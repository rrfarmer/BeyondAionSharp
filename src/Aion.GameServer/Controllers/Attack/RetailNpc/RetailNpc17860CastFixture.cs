using System.Runtime.CompilerServices;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Explicit offline-only 17860 cast integration. It does not discover
/// targets, prove live source timing, or admit the skill into gameplay.</summary>
internal static class RetailNpc17860CastFixture
{
    // Static fireball geometry only. Width/depth are explicitly supplied native
    // actor fields, not host bound radii; world, LOS and target restrictions
    // remain the caller's responsibility. This is not a gameplay selector.
    internal static bool InFireballForOfflineFixture(float centerX,float centerY,float centerZ,
        float targetX,float targetY,float targetZ,float casterWidth,float casterDepth,float targetWidth,float targetDepth)
    {
        if(!float.IsFinite(centerX)||!float.IsFinite(centerY)||!float.IsFinite(centerZ)
            ||!float.IsFinite(targetX)||!float.IsFinite(targetY)||!float.IsFinite(targetZ)
            ||!float.IsFinite(casterWidth)||!float.IsFinite(casterDepth)||!float.IsFinite(targetWidth)||!float.IsFinite(targetDepth)
            ||casterWidth<0||casterDepth<0||targetWidth<0||targetDepth<0)
            throw new ArgumentOutOfRangeException(nameof(casterWidth),"Fixture requires finite positions and nonnegative native actor dimensions");
        float lower=centerZ-7f,upper=centerZ+7f;
        float radius=MathF.Max(casterWidth,casterDepth)*0.5f+3f+MathF.Max(targetWidth,targetDepth)*0.5f;
        float dx=targetX-centerX,dy=targetY-centerY;
        return lower<=targetZ && targetZ<=upper && dy*dy+dx*dx<radius*radius;
    }

    // The source profile models NPC class construction; candidate player
    // dimensions remain explicit because current actor state is not observed.
    internal static bool InSourceFireballForOfflineFixture(SkyTempleNpcProfile caster,
        float centerX,float centerY,float centerZ,float targetX,float targetY,float targetZ,
        float playerWidth,float playerDepth)
    {
        ArgumentNullException.ThrowIfNull(caster);
        return InFireballForOfflineFixture(centerX,centerY,centerZ,targetX,targetY,targetZ,
            caster.Bounds.Front,caster.Bounds.Side,playerWidth,playerDepth);
    }

    private sealed class State(Npc npc,Player first,SkyTempleNpcProfile profile,
        RetailNpc17860PrimaryProjection.Candidate candidate,byte[] source,ushort word,
        Func<Player,RetailNpc17860DefenseEffect.Inputs?> inputs,Func<Player,bool> current,Func<Player,int,int,int> draw)
    {
        internal readonly Npc Npc=npc;
        internal readonly Player First=first;
        internal readonly SkyTempleNpcProfile Profile=profile;
        internal readonly RetailNpc17860PrimaryProjection.Candidate Candidate=candidate;
        internal readonly byte[] Source=source;
        internal readonly ushort Word=word;
        internal readonly Func<Player,RetailNpc17860DefenseEffect.Inputs?> Inputs=inputs;
        internal readonly Func<Player,bool> Current=current;
        internal readonly Func<Player,int,int,int> Draw=draw;
        internal readonly List<Effect> Effects=[];
        internal int Started;
    }
    private static readonly ConditionalWeakTable<Skill,State> Fixtures=new();

    internal static void AttachFromCatalogForOfflineFixture(Skill skill,RetailNpc17860PrimaryProjection.Candidate candidate,
        ReadOnlySpan<byte> source,Func<Player,RetailNpc17860DefenseEffect.Inputs?> inputs,Func<Player,bool> current,
        Func<Player,int,int,int> draw)
    {
        ArgumentNullException.ThrowIfNull(skill);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(draw);
        _=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source);
        if(skill.GetEffector() is not Npc npc ||skill.GetFirstTarget() is not Player first ||skill.GetSkillId()!=17860
            ||!npc.IsSpawned() ||npc.IsDead() ||!first.IsSpawned() ||first.IsDead()
            ||!ReferenceEquals(npc.GetWorldMapInstance(),first.GetWorldMapInstance())
            ||!RetailNpcStatCatalog.TryGet(npc,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,17860,skill.GetSkillLevel(),out var original)
            ||original.Supported ||original.Sha256!="52953fccf5f044eb35e7ab1fdebb06af5ff5948ccd67dacecf6aacb855367ee2"
            ||candidate.Primary.Id!=17860
            ||!RetailNpcStatCatalog.TryGetSkillTiming(npc,17860,skill.GetSkillLevel(),out var timing))
            throw new InvalidOperationException("Fixture cast has no selected Kasika source and timing");
        Fixtures.Add(skill,new(npc,first,profile,candidate,source.ToArray(),timing.Word,inputs,current,draw));
    }

    internal static bool TryPrepareForOfflineFixture(Skill skill,out int word)
    {
        word=0;if(!Fixtures.TryGetValue(skill,out var state))return false;
        if(!ReferenceEquals(skill.GetEffector(),state.Npc) ||!ReferenceEquals(skill.GetFirstTarget(),state.First)
            ||!RetailNpcStatCatalog.TryGet(state.Npc,out var current) ||!ReferenceEquals(current,state.Profile)
            ||!state.Npc.IsSpawned() ||state.Npc.IsDead() ||!state.First.IsSpawned() ||state.First.IsDead()
            ||Interlocked.Exchange(ref state.Started,1)!=0)
            throw new InvalidOperationException("Fixture Kasika cast source or lifetime changed");
        word=state.Word;return true;
    }

    internal static void BeforeInitializationForOfflineFixture(Skill skill,Effect effect)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return;
        if(Volatile.Read(ref state.Started)!=1 ||!ReferenceEquals(effect.GetEffector(),state.Npc)
            ||effect.GetEffected() is not Player player ||!player.IsSpawned() ||player.IsDead()
            ||!ReferenceEquals(player.GetWorldMapInstance(),state.Npc.GetWorldMapInstance()))
            throw new InvalidOperationException("Fixture Kasika target or caster changed");
        lock(state.Effects)state.Effects.Add(effect);
        RetailNpc17860PrimaryProjection.AttachInitializationForOfflineFixture(effect,state.Candidate,(lo,hi)=>state.Draw(player,lo,hi));
    }

    internal static void AfterInitializationForOfflineFixture(Skill skill,Effect effect)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return;
        lock(state.Effects)if(!state.Effects.Contains(effect))throw new InvalidOperationException("Unknown fixture Kasika effect");
        if(!RetailNpc17860PrimaryProjection.TryGetInitializationForOfflineFixture(effect,out bool handled,out bool success))
            throw new InvalidOperationException("Fixture Kasika initialization did not run");
        if(!handled ||!success)return;
        if(effect.GetEffected() is not Player player ||!effect.IsInSuccessEffects(2))
            throw new InvalidOperationException("Fixture Kasika defense was not selected");
        RetailNpc17860DefenseEffect.AttachDynamicForOfflineFixture(effect,state.Source,state.Inputs,state.Current);
        RetailNpcStatSpellDrain.AttachKasikaTimelineForOfflineFixture(effect);
    }

    internal static Effect[] ObservedEffectsForOfflineFixture(Skill skill)
    {
        if(!Fixtures.TryGetValue(skill,out var state))return [];
        lock(state.Effects)return state.Effects.ToArray();
    }
    internal static bool IsSelectedForOfflineFixture(Skill skill)
        =>Fixtures.TryGetValue(skill,out var state) &&Volatile.Read(ref state.Started)==1;
}
