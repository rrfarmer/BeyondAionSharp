using System.Collections.Immutable;
using System.Globalization;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion;
using M = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using P = Aion.GameServer.GeoEngine.RetailPath.RetailNpc48PresentationInputs;
using V = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedVisibility;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceCatalog;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive whole completed-motion delivery calculation under the named4.8 presentation policy.
/// No socket, world mutation, actor lookup, clock generation, or once-ledger consumption occurs.</summary>
internal static class RetailNpc48CompletedPackets
{
    internal const string NpcRuntime = "Aion.GameServer.Model.GameObjects.Npc";
    internal const string PlayerRuntime = "Aion.GameServer.Model.GameObjects.Players.Player";
    internal const string SpawnRuntime = "Aion.GameServer.Model.Templates.Spawns.SpawnTemplate";
    internal const string XmlZoneRuntime = "Aion.GameServer.Model.Templates.Zone.ZoneTemplate";
    internal const string MaterialZoneRuntime = "Aion.GameServer.Model.Templates.Zone.MaterialZoneTemplate";
    internal sealed record Limits(int MaxDeliveries,long MaxEncodedBytes,int MaxSteps);
    internal sealed record Step(int Sequence,uint RecipientId,string Stage,string Source,string? Detail=null,long? Value=null);
    internal enum Status { EncodedWholeBatch, InvalidInput, UnsupportedReachedMeaning }
    // Both encoded factories return immutable packet snapshots; the old delivery record
    // is intentionally limited to its move/delete SnapshotPacket subtype.
    internal sealed record Delivery(uint RecipientId,string RecipientLifetime,string ConnectionGeneration,
        uint ObjectId,AionServerPacket Packet);
    internal sealed record Result(Status Status,ImmutableArray<Delivery> Deliveries,
        int? FailureIndex,P.Failure? Failure,ImmutableArray<Step> KnownReads,string SourceSnapshotSha256,
        string Operation,string Epoch,string? PresentationSnapshotSha256)
    {
        internal bool HostEffectsExecuted=>false;
        internal bool OnceLedgerConsumed=>false;
        internal bool CurrentHostObserved=>false;
        internal bool ActivationReady=>false;
        internal bool CanApplyToHost=>false;
    }
    internal static Result Calculate(M.Candidate candidate,O.Input original,P.Capability? presentation,Limits limits)
    {
        ArgumentNullException.ThrowIfNull(candidate);ArgumentNullException.ThrowIfNull(original);
        var replay=new Replay(candidate,original,presentation,limits);
        try{return replay.Run();}
        catch(P.Fault error){return replay.Failed(error.Code,error.Message);}
        catch(O.Fault error){return replay.Failed(error.Code,error.Message);}
        catch(RetailMainApplyFault error){return replay.Failed("source-motion",error.Message);}
        catch(GI.Domain error){return replay.Failed("item-source",error.Message);}
        catch(Exception error) when(error is ArithmeticException or ArgumentException or InvalidOperationException)
        {return replay.Failed("malformed",error.GetType().Name+": "+error.Message);}
    }

    private sealed class Replay(M.Candidate candidate,O.Input original,P.Capability? presentation,Limits limits)
    {
        private readonly List<Step> steps=[];
        private readonly ImmutableArray<Delivery>.Builder deliveries=ImmutableArray.CreateBuilder<Delivery>();
        private M.CandidateData Value=>candidate.Value;
        private P.Raw Raw=>presentation!.Original;
        private P.Npc HostNpc=>Raw.Npc;
        private P.Sources Sources=>presentation!.Source;
        private int index=-1,clockIndex;
        private uint recipient;
        private long byteCount;
        private bool reachedPresentation;
        private void Note(string stage,string source,string? detail=null,long? value=null)
        {P.Need(steps.Count<limits.MaxSteps,"budget","Read/decision trace bound.");steps.Add(new(steps.Count+1,recipient,stage,source,detail,value));}
        internal Result Failed(string code,string detail)=>new(index<0?Status.InvalidInput:Status.UnsupportedReachedMeaning,[],
            index<0?null:index,new(code,detail),[..steps],Value.SourceSnapshotSha256,Value.Operation,Value.Epoch,presentation?.SnapshotSha256);
        internal Result Run()
        {
            P.Need(limits is not null&&limits.MaxDeliveries is >=0 and <=65536&&limits.MaxEncodedBytes is >=0 and <=536870912
                &&limits.MaxSteps is >0 and <=1000000,"budget","Explicit finite output/trace capacities required.");
            P.Need(original.Npc is not null&&original.Scene is not null&&original.Npc.Key is not null&&original.Npc.Image is not null
                &&original.Npc.Template is not null&&original.Npc.MotionSource is not null&&original.Npc.MotionSource.Motion is not null
                &&original.Npc.MotionSource.Template1848 is not null&&original.Npc.MotionSource.Suppression1a58 is not null
                &&original.Scene.Geometry is not null&&original.Scene.Globals is not null&&original.Scene.Lease is not null&&original.Evidence is not null,
                "input","Missing original completed-motion ownership root.");
            // Hash reconstruction validates the complete original owned input; it does not consume the owner.
            M.Owner owner=M.Owner.FromDeclaredInput(original);
            P.Need(owner.SourceSnapshotSha256==Value.SourceSnapshotSha256&&owner.Generation==Value.ExpectedWorldGeneration
                &&owner.ActorLifetime==Value.SourceActorLifetime&&owner.Operation==Value.Operation&&owner.Epoch==Value.Epoch,
                "stale-motion","Completed private candidate does not belong to this exact original source hash/operation/epoch.");
            P.Need(!owner.Consumed&&Value.State is not null&&!Value.State.ProvisionalIntents.IsDefault,"input","Complete private motion and original owner required.");
            Note("ReconstructedOriginalMHash","M.Owner.FromDeclaredInput",Value.SourceSnapshotSha256);
            for(index=0;index<Value.State.ProvisionalIntents.Length;index++)
            {
                V.Intent intent=Value.State.ProvisionalIntents[index];recipient=intent.Meaning.RecipientId;
                P.Need(intent.Meaning.ObjectId==original.Npc.Key.ObjectId,"identity","Only this completed source NPC may be encoded.");
                var user=original.Scene.Users.SingleOrDefault(u=>u.Key.ObjectId==recipient);
                P.Need(user is not null&&user.Key.Lifetime==intent.RecipientLifetime&&user.ConnectionGeneration==intent.ConnectionGeneration
                    &&user.ConnectionPresent==intent.HasConnection,"recipient","Original actor/connection generation differs from completed intent.");
                // No connection skips every presentation source/field/clock child, even an unavailable capability.
                if(!intent.HasConnection){Note("NoConnectionSkip","SendBuf/managed transport boundary");continue;}
                AionServerPacket packet;int bytes;
                if(intent.Meaning is V.Spawn spawn)
                {
                    BeginPresentation(user);
                    RetailNpc48SpawnPacketBody.Fields fields=SpawnFields(spawn,user);
                    var body=RetailNpc48SpawnPacketBody.Calculate(fields);
                    P.Need(body.Status==RetailNpc48SpawnPacketBody.Status.Encoded,"packet",body.Failure??"Spawn body not complete.");
                    bytes=body.Bytes.Length;packet=body.CreatePacket();
                }
                else
                {
                    var body=RetailNpc48MoveDeletePackets.CalculateBody(intent);
                    P.Need(body.Status==RetailNpc48MoveDeletePackets.Status.Encoded,"meaning",body.Failure??"Unsupported connected move/delete meaning.");
                    bytes=body.Bytes.Length;packet=RetailNpc48MoveDeletePackets.SnapshotPacket.FromEncodedBody(body);
                }
                P.Need(deliveries.Count<limits.MaxDeliveries&&byteCount<=limits.MaxEncodedBytes-bytes,"budget","Whole encoded batch bound.");
                byteCount+=bytes;deliveries.Add(new(recipient,intent.RecipientLifetime,intent.ConnectionGeneration,intent.Meaning.ObjectId,packet));
                Note("ProvisionalEncodedBody","Existing4.8 packet infrastructure",value:bytes);
            }
            if(reachedPresentation)P.Need(clockIndex==Raw.OriginalBirthClocks.Length,"clock","Unused original birth-clock tail.");
            Note("WholeBatchCompleteNoOnceConsumption","Named4.8 compatibility policy",value:deliveries.Count);
            return new(Status.EncodedWholeBatch,deliveries.ToImmutable(),null,null,[..steps],Value.SourceSnapshotSha256,
                Value.Operation,Value.Epoch,presentation?.SnapshotSha256);
        }
        private void BeginPresentation(O.User user)
        {
            P.Need(presentation is not null,"presentation","Connected spawn requires its complete source/current4.8 presentation capability.");
            P.Evidence e=Raw.Evidence;
            P.Need(e.Operation==Value.Operation&&e.Epoch==Value.Epoch&&e.Policy==P.Policy&&e.RetailPackageContext==O.PackageContext,
                "context","Presentation source and completed operation do not share an epoch/context.");
            P.Need((e.Origin==P.Origin.ConstructedSourceFixture)==(original.Evidence.Origin==O.Origin.ConstructedSourceFixture),
                "origin","Fixture and declared-snapshot origins cannot be silently relabelled.");
            Bind(HostNpc.Binding,original.Npc.Key.ObjectId,Value.SourceActorLifetime);
            P.Need(HostNpc.RuntimeType==NpcRuntime&&HostNpc.OriginalTemplateId.HasValue,"class","Actual exact ordinary4.8 Npc template owner required.");
            P.Need(HostNpc.OriginalTransformModel==P.Reference.Null,"transform","Active/unknown4.8 NPC transformation has no admitted ordinary visual mapping.");
            P.World world=Raw.World;
            P.Need(world.Identity==original.Scene.Geometry.Identity&&world.Generation==Value.ExpectedWorldGeneration
                &&world.MapId==P.MapId&&original.Scene.Geometry.BaseId24==P.MapId&&world.SourceGeneration==Sources.SnapshotSha256,
                "world","Actual retained host map/world/source generation must join the completed motion.");
            var player=Raw.Players.SingleOrDefault(p=>p.Binding.ObjectId==user.Key.ObjectId);
            P.Need(player is not null&&player.RuntimeType==PlayerRuntime,"class","Reached recipient has no exact actual4.8 Player declaration.");
            Bind(player.Binding,user.Key.ObjectId,user.Key.Lifetime);reachedPresentation=true;
            Note("PresentationOrigin","Original immutable managed field declaration",e.Origin+";host capture unavailable");
        }
        private void Bind(P.Binding b,uint id,string life)
        {
            P.Need(b.ObjectId==id&&b.Lifetime==life&&b.World==original.Scene.Geometry.Identity&&b.WorldGeneration==Value.ExpectedWorldGeneration
                &&b.MapId==P.MapId&&b.InstanceId==Raw.World.InstanceId&&b.SourceGeneration==Sources.SnapshotSha256,
                "binding","Object/source/instance/lifetime/world generation mismatch.");
            P.Text(b.Lifetime,"lifetime");
        }
        private int Field(int? value,string field)
        {P.Need(value.HasValue,"unknown","Unknown reached raw4.8 field "+field);Note("ReadOriginal48Field",field,value:value.Value);return value.Value;}
        private byte CreatureType(P.Player player)
        {
            P.OptionalType? field=HostNpc.OriginalOverriddenType;
            P.Need(field is not null&&field.Reference is P.Reference.Null or P.Reference.Present,"unknown","Unknown original overriddenType field.");
            byte type;
            if(field.Reference==P.Reference.Present)
            {
                type=field.OriginalEnumToken switch {"ATTACKABLE"=>0,"PEACE"=>2,"AGGRESSIVE"=>8,"INVULNERABLE"=>10,"FRIEND"=>38,"SUPPORT"=>54,
                    _=>throw new P.Fault("enum","Unknown consumed CreatureType token.")};
                Note("ReadOriginal48Enum","Npc.overriddenType",field.OriginalEnumToken);
            }
            else
            {
                P.Need(field.OriginalEnumToken is null,"input","Null overriddenType cannot carry another value.");
                if(IsNone(player))type=2;
                else if(IsAggressive(player))type=8;
                else
                {
                    // All admitted source tribes have baseMONSTER and actual untransformed players have PC/PC_DARK.
                    // IsHostile's unconditional MONSTER branch precedes friend/neutral/support; no assumed relationship answer.
                    P.Need(Sources.Base(NpcTribe())=="MONSTER","source","Unsupported source base tribe.");
                    string targetBase=Sources.Base(PlayerTribe(player));
                    P.Need(targetBase is "PC" or "PC_DARK","source","Unsupported actual target base tribe.");
                    type=0;Note("CalculatedRelation","TribeRelationService.isHostile:MONSTER");
                }
            }
            int custom=Field(player.OriginalCustomStateField,"Player.customState");
            if((custom&64)!=0&&type is not 0 and not 8)return 0;
            if((custom&256)!=0&&type is 0 or 8)return 2;
            return type;
        }
        private string NpcTribe()
        {
            P.Need(Field(HostNpc.OriginalCreatorIdField,"Npc.creatorId")==0,"creator","Positive creator needs actual creator lookup/tribe ownership.");
            P.Need(HostNpc.OriginalTransformModel==P.Reference.Null,"transform","Unclosed source NPC transform tribe.");
            string tribe=Sources.Npc(checked((uint)HostNpc.OriginalTemplateId!.Value)).Tribe;
            Note("ReadSelected48Source","NpcTemplate.tribe",tribe);return tribe;
        }
        private string PlayerTribe(P.Player player)
        {
            P.Need(player.OriginalTransformModel==P.Reference.Null,"transform","Active/unknown Player transformation requires its original source/tribe owner.");
            Note("ReadOriginal48Enum","Player.race",player.OriginalRaceToken);
            return player.OriginalRaceToken switch {"ELYOS"=>"PC","ASMODIANS"=>"PC_DARK",_=>throw new P.Fault("enum","Unknown ordinary Player race token.")};
        }
        private bool DataRelation(string list,P.Player player)=>Sources.Relation(list,NpcTribe(),PlayerTribe(player));
        private bool IsNone(P.Player player)
        {
            if(DataRelation("aggro",player))return false;
            if(CheckSiegeRelation(player)||DataRelation("hostile",player)||DataRelation("neutral",player))return false;
            P.Need(Sources.Base(NpcTribe())=="MONSTER","source","Outside finite isNone base family.");
            return DataRelation("none",player);
        }
        private bool CheckSiegeRelation(P.Player player)
        {
            // Selected source declarations have default AbyssNpcType.NONE. Actual ordinary spawn type is still read.
            P.Spawn? spawn=HostNpc.OriginalSpawn;
            P.Need(spawn is not null&&spawn.Kind is P.SpawnKind.Absent or P.SpawnKind.OrdinarySpawnTemplate,
                "spawn","Unknown/custom spawn producer in the reached siege relation.");
            P.Need(Sources.Base(NpcTribe())=="MONSTER","source","Siege relation outside finite MONSTER family.");
            Note("CalculatedSiegeFalse","TribeRelationService.checkSiegeRelation","sourceNONE/ordinary spawn;MONSTER base");return false;
        }
        private bool IsAggressive(P.Player player)
        {
            string tribe=NpcTribe();
            P.Need(tribe is "MONSTER" or "ELEMENTAL_WATER" or "ELEMENTAL_EARTH" or "ELEMENTAL_AIR" or "ELEMENTAL_FIRE",
                "source","Unsupported special aggressive tribe program.");
            P.Need(Sources.Base(NpcTribe())=="MONSTER","source","Unsupported aggressive base tribe.");
            P.Need(player.OriginalPanesterraFaction is P.Reference.Null or P.Reference.Present,"unknown","Reached original Player.panesterraFaction reference unknown.");
            Note("ReadOriginal48Reference","Player.panesterraFaction",player.OriginalPanesterraFaction.ToString());
            // Preserve the getter-first short circuit even though the source is not GAB1_. No faction value is guessed.
            if(player.OriginalPanesterraFaction==P.Reference.Present)
                P.Need(!NpcTribe().StartsWith("GAB1_",StringComparison.Ordinal),"source","Active faction tribe owner.");
            return DataRelation("aggro",player);
        }
        private ImmutableArray<RetailNpc48SpawnPacketBody.GearEntry> Gear()
        {
            P.Equipment? raw=HostNpc.OriginalOverrideEquipment;
            P.Need(raw is not null&&raw.Kind!=P.EquipmentKind.Unknown,"unknown","Unknown original overrideEquipment root.");
            if(raw.Kind==P.EquipmentKind.NoOverride)
            {Note("SelectedTemplateEquipmentAbsent","Npc.getOverrideEquipment/NpcTemplate","all25 original source declarations absent");return [];}
            P.Need(raw.Kind==P.EquipmentKind.OriginalOrderedOverride&&!raw.OriginalOrderedTemplates.IsDefault
                &&raw.OriginalOrderedTemplates.Length<=1024&&raw.ConstructorProgram=="NpcEquippedGear(NpcEquipmentList)",
                "equipment","Complete original ordered current gear constructor recipe required.");
            P.Text(raw.Owner,"equipment owner");P.Text(raw.Generation,"equipment generation");
            var selected=new Dictionary<ItemSlot,C.Catalog.Record>();
            foreach(var record in raw.OriginalOrderedTemplates)
            {
                P.Need(record is not null&&record.Metadata.Key.Authority==C.Authority.CSharp&&record.Metadata.Key.Family==C.Family.Item,
                    "equipment","Every original item reference must resolve through its private retained4.8 source catalog.");
                P.Need(Raw.Evidence.Origin==P.Origin.ConstructedSourceFixture||record.Owner.SourceOrigin==C.Origin.SelectedSourceDeclaration,
                    "origin","A synthetic item catalog cannot be relabelled as real declared host content.");
                var item=record.Read();string? group=(string?)item.Attribute("item_group");group??="NONE";
                P.Need(Enum.GetNames<ItemGroup>().Contains(group,StringComparer.Ordinal),"enum","Unknown consumed exact ItemGroup source token.");
                ItemGroup actual=Enum.Parse<ItemGroup>(group,false);long mask=actual.GetValidEquipmentSlots();
                foreach(ItemSlot slot in ItemSlotExtensions.GetSlotsFor(mask))if(!selected.ContainsKey(slot))
                {selected.Add(slot,record);Note("CalculatedFirstFreeSlot","NpcEquippedGear.init",record.Identity+"#"+record.Metadata.RawSha256,value:(long)slot);break;}
            }
            return [..selected.OrderBy(x=>Array.IndexOf(SingleSlots,x.Key)).Select(x=>new RetailNpc48SpawnPacketBody.GearEntry(
                unchecked((ulong)(long)x.Key),uint.Parse(x.Value.Metadata.Key.Primary,CultureInfo.InvariantCulture)))];
        }
        private static readonly ItemSlot[] SingleSlots=ItemSlotExtensions.GetSlotsFor(long.MaxValue);
        private ushort StaticId()
        {
            P.Spawn? spawn=HostNpc.OriginalSpawn;
            P.Need(spawn is not null&&spawn.Kind!=P.SpawnKind.Unknown,"unknown","Unknown actual spawn field.");
            if(spawn.Kind==P.SpawnKind.Absent)return 0;
            P.Need(spawn.Kind==P.SpawnKind.OrdinarySpawnTemplate&&spawn.RuntimeType==SpawnRuntime,"spawn","Unclosed spawn subclass/current getter.");
            P.Text(spawn.Owner,"spawn owner");P.Text(spawn.Generation,"spawn generation");
            return unchecked((ushort)Field(spawn.OriginalStaticIdField,"SpawnTemplate._staticId"));
        }
        private uint Town()
        {
            P.Spawn? spawn=HostNpc.OriginalSpawn;
            P.Need(spawn is not null&&spawn.Kind is P.SpawnKind.Absent or P.SpawnKind.OrdinarySpawnTemplate,
                "town","Unclosed TownSpawnTemplate precedence.");
            P.Need(HostNpc.OriginalPositionSpawnedField.HasValue,"unknown","Unknown original WorldPosition._isSpawned field.");
            Note("ReadOriginal48Field","WorldPosition._isSpawned",value:HostNpc.OriginalPositionSpawnedField.Value?1:0);
            if(!HostNpc.OriginalPositionSpawnedField.Value)return 0;
            P.World world=Raw.World;
            // This is a source-wide proof, not a supplied future findZones() result or Town0 answer.
            P.Need(world.RegistryProgram=="ZoneService.CreateZoneInstances"&&world.SourceGeneration==Sources.SnapshotSha256
                &&!world.OriginalCompleteRegistrations.IsDefault&&world.OriginalCompleteRegistrations.Length<=65536,
                "zone","Complete actual current zone/material registrations and source program required.");
            P.Text(world.RegistryGeneration,"zone registry generation");
            var names=new HashSet<string>(StringComparer.Ordinal);var owners=new HashSet<string>(StringComparer.Ordinal);
            foreach(var root in world.OriginalCompleteRegistrations)
            {
                P.Need(root is not null,"zone","Null current zone registration.");P.Text(root.Owner,"zone owner");P.Text(root.Generation,"zone generation");
                P.Need(owners.Add(root.Owner)&&root.OriginalMapId==P.MapId&&root.ConstructorSourceGeneration==Sources.SnapshotSha256
                    &&root.CurrentTemplateGeneration==root.Generation,"zone","Current registration identity/source generation differs from the reviewed constructor recipe.");
                if(root.Producer==P.ZoneProducer.XmlZoneTemplate)
                {
                    P.Need(root.RuntimeTemplateType==XmlZoneRuntime&&root.OriginalName is not null&&Sources.CurrentXmlZoneNames.Contains(root.OriginalName)
                        &&names.Add(root.OriginalName),"zone","Unbound/duplicate XML source zone.");
                }
                else if(root.Producer==P.ZoneProducer.MaterialZoneTemplate)
                {
                    P.Need(root.RuntimeTemplateType==MaterialZoneRuntime,"zone","Unknown material template subclass/producer.");
                    P.Text(root.OriginalName,"material geometry name");P.Text(root.MaterialGeometryOwner,"material geometry owner");P.Text(root.MaterialGeometryGeneration,"material geometry generation");
                    // The exact current constructor sets map/name/shape but never writes inherited TownId.
                    // Its complete source/current recipe is required, with no later generation substituted.
                }
                else throw new P.Fault("zone","Unknown/custom current zone producer.");
                P.Need(root.OriginalTownIdField==0,"zone","Current inherited town field differs from its computed zero source/constructor value.");
                Note("VerifiedCurrentZoneConstructor","ZoneTemplate.TownId/MaterialZoneTemplate",root.Owner);
            }
            P.Need(names.SetEquals(Sources.CurrentXmlZoneNames),"zone","Actual current registry omits a source map zone.");
            Note("CalculatedNoTown","TownService.getTownIdByPosition","six currentXML roots plus complete known material constructors; no synthetic Java world zone");
            return 0;
        }
        private byte SpawnFlag(O.User user)
        {
            P.Need(HostNpc.OriginalLastSpawnMillis.HasValue,"unknown","Unknown actual lastSpawn timestamp.");
            P.Need(clockIndex<Raw.OriginalBirthClocks.Length,"clock","Missing reached original4.8 birth clock.");
            P.Clock clock=Raw.OriginalBirthClocks[clockIndex++];
            P.Need(clock is not null&&clock.ObjectId==original.Npc.Key.ObjectId&&clock.ObjectLifetime==Value.SourceActorLifetime
                &&clock.RecipientId==user.Key.ObjectId&&clock.RecipientLifetime==user.Key.Lifetime&&clock.ConnectionGeneration==user.ConnectionGeneration
                &&clock.Operation==Value.Operation&&clock.Epoch==Value.Epoch,"clock","Original birth-clock order/actor/connection/epoch mismatch.");
            P.Text(clock.Receipt,"original host clock receipt");
            long elapsed=unchecked(clock.UnixMillis-HostNpc.OriginalLastSpawnMillis.Value);
            Note("OriginalBirthClock","Creature.isNewSpawn:System.currentTimeMillis",clock.Receipt,value:clock.UnixMillis);
            // All25 source type attributes are absent; isFlag is false. Keep Java signed64 wrap and strict boundary.
            return elapsed<1500?(byte)1:(byte)0;
        }
        private uint Target(uint target)
        {
            if(target==0)return 0;
            P.ObjectBinding? raw=Raw.OriginalTargetObjects.SingleOrDefault(x=>x.ObjectId==target);
            P.Need(raw is not null&&raw.Disposed==false&&raw.World==Raw.World.Identity&&raw.WorldGeneration==Raw.World.Generation
                &&raw.MapId==Raw.World.MapId&&raw.InstanceId==Raw.World.InstanceId,"target","Actual nonzero target lifetime/world binding unavailable.");
            string? life=target==original.Npc.Key.ObjectId?original.Npc.Key.Lifetime:original.Scene.Users.SingleOrDefault(x=>x.Key.ObjectId==target)?.Key.Lifetime;
            string type=target==original.Npc.Key.ObjectId?NpcRuntime:PlayerRuntime;
            P.Need(life is not null&&life==raw.Lifetime&&raw.RuntimeType==type,"target","Target is outside the actual completed owned actor graph.");
            Note("BoundCurrentTarget","Retail current130c + original4.8 object lifetime",raw.Lifetime,value:target);return target;
        }
        private RetailNpc48SpawnPacketBody.Fields SpawnFields(V.Spawn spawn,O.User user)
        {
            P.Need(!spawn.Arguments.IsDefault&&spawn.Arguments.Length==38&&spawn.Arguments.Select(x=>x.NativeIndex).SequenceEqual(Enumerable.Range(2,38)),
                "meaning","Complete ordered38 native arguments required.");
            V.Argument Arg(int n,V.ArgumentKind kind)
            {var a=spawn.Arguments[n-2];P.Need(a is not null&&a.NativeIndex==n&&a.Kind==kind,"meaning","Native argument shape mismatch.");return a;}
            uint U(int n){ulong value=Arg(n,V.ArgumentKind.Unsigned).Scalar;P.Need(value<=uint.MaxValue,"meaning","Native unsigned argument width.");return (uint)value;}
            uint F(int n){ulong value=Arg(n,V.ArgumentKind.FloatBits).Scalar;P.Need(value<=uint.MaxValue,"meaning","Native float argument width.");return (uint)value;}
            RetailMainVectorWords Vector(int n){var a=Arg(n,V.ArgumentKind.Vector);P.Need(a.Vector.HasValue,"meaning","Unknown original vector argument.");return a.Vector.Value;}
            P.NpcContent content=Sources.Npc(U(4));
            P.Need(content.Id==HostNpc.OriginalTemplateId&&U(3)==original.Npc.Key.ObjectId,"template","Actual retained template/native class identity mismatch.");
            P.Need(Arg(13,V.ArgumentKind.Utf16).Text=="","custom-name","Active native custom name has no admitted ordinary4.8 field.");
            P.Need(U(11)==0&&U(12)==0&&U(29)==0&&U(31)==0&&U(32)==0&&U(35)==0,
                "native-extra","Active unrepresentable ordinary spawn extension requires its gameplay/presentation mapping.");
            P.Need(O.U8(Value.State.Image,0x16c9)==0,"visual","Active native variant override needs its own retained visual mapping.");
            P.Need(O.U32(Value.State.Image,0x1940)==1,"class","Only the actual ordinary native kind1 producer is admitted.");
            uint hp=O.U32(Value.State.Image,0x778);int max=unchecked((int)U(17));
            P.Need(max>0&&hp is >0 and <=int.MaxValue&&hp<=max&&U(16)==(ulong)hp*99/(uint)max+1,
                "life","Actual positive nonoverfull same-epoch HP/max/retail percent required.");
            ushort level=O.U16(Value.State.Image,0x1492);
            P.Need(level is >=1 and <=100&&U(18)==level,"level","Effective ordinary level must be valid before byte narrowing.");
            uint radius=F(20),height=F(21),speed=F(22);
            P.Need(RetailMainApplyMath.Float(radius)>0&&RetailMainApplyMath.Float(height)>0&&RetailMainApplyMath.Float(speed)>=0,
                "bounds","Current effective ordinary positive bounds/nonnegative speed required.");
            byte heading=checked((byte)U(8)),mask=checked((byte)U(27));
            P.Need(heading<120&&(mask&0x10)==0,"motion","Unsupported ordinary heading/vehicle meaning.");
            P.Player player=Raw.Players.Single(x=>x.Binding.ObjectId==user.Key.ObjectId);
            byte type=CreatureType(player);ushort state=unchecked((ushort)Field(HostNpc.OriginalStateField,"Creature.state"));
            int creator=Field(HostNpc.OriginalCreatorIdField,"Npc.creatorId");P.Need(creator==0,"creator","Positive creator lookup/house presentation unsupported.");
            P.Need(HostNpc.OriginalMasterNameField is {Known:true},"unknown","Unknown actual masterName field.");string? master=HostNpc.OriginalMasterNameField.Units;
            Note("ReadOriginal48Field","Npc.masterName",master);
            var gear=Gear();byte birth=SpawnFlag(user);ushort staticId=StaticId();
            byte visual=unchecked((byte)Field(HostNpc.OriginalVisualStateField,"Creature.visualState"));
            uint target=Target(U(37));uint town=Town();
            Note("ComputedRetained48Presentation","Named mapping policy",content.Name+";native tagged name/23 slots are not4.8 content");
            return new(Vector(2),spawn.ObjectId,content.Id,content.Id,type,state,heading,content.NameId,content.TitleId,
                unchecked((uint)creator),master,checked((byte)U(16)),max,(byte)level,gear,radius,height,speed,checked((ushort)U(23)),checked((ushort)U(24)),
                birth,Vector(26),mask,staticId,visual,1,target,town);
        }
    }
}
