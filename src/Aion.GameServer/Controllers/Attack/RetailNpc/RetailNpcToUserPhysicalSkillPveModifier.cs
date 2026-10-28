using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive complete raw-input NPC-to-User PvE modifier chain. Physical skill remains
/// the default; normal main-hand and magical skill entries require their separate proof.
/// Computes snapshot/live defense, source slot190, User HP2 and the existing ratio helper.
/// This is not a hit/skill calculator, live input adapter, gear refresh or HP application.
/// </summary>
internal static class RetailNpcToUserPhysicalSkillPveModifier
{
    internal const string ProofSha256="59ceffa6cb28fddaba0a21fbfbd152ec20aff7802a0935f3064d6b1fc51527de";
    internal const string OracleAmendmentSha256="2b2a6ee87ba35b29f131055457debc31f7de936213062a175e2a265496d2a60a";
    internal const string ReleaseEvidenceSha256="489b36c4d0ca333906f2942cdcbf61c8c87a929ecb0810373052547e3e83933a";
    internal const ulong NpcVtable=0x1412b8470,UserVtable=0x141161250;
    internal const string ModeSourceProofSha256="a14e930a1ba71ef064682536c2e13718349a6159ebec20ff060cec3b3887e84e";
    internal enum NativeEntry { PhysicalSkill, MagicalSkill, NormalAttack }
    internal enum Origin { Unknown, MathematicalFixture, SourceBoundDeclaration, ObservedOriginal }
    internal sealed record Context(string MainSha256,string PackageContext,string Operation,string Epoch,
        Origin Origin,string Receipt,string ProofSha256,string OracleAmendmentSha256,string ReleaseEvidenceSha256);
    internal sealed record Binding(ulong Address,string AllocationIdentity,string Generation,Context Context);
    internal sealed record Floating(bool? NearestEven,bool? ExceptionsMasked,bool? FlushToZero,bool? DenormalsAreZero);
    internal sealed record NpcRaw
    {
        internal int? Type144 {get;init;}
        internal ulong? Vtable {get;init;}
        internal uint? Kind1940 {get;init;}
        internal uint? Master188c {get;init;}
        internal uint? Related1894 {get;init;}
        internal ulong? Template1848 {get;init;}
        internal ulong? Virtual1850 {get;init;}
        internal ulong? Difficulty1bf8 {get;init;}
        internal int? Current1bc8 {get;init;}
        internal int? Current1bd8 {get;init;}
        internal int? Template23c {get;init;}
        internal int? Current1bd0 {get;init;}
        internal int? Current1be0 {get;init;}
        internal int? Template244 {get;init;}
        internal int? NormalAttackElement8f4 {get;init;}
    }
    internal sealed record UserRaw
    {
        internal int? Type144 {get;init;}
        internal ulong? Vtable {get;init;}
        internal ulong? Primary6e50 {get;init;}
        internal ulong? Backup6e38 {get;init;}
        internal int? Current166c {get;init;}
        internal int? CurrentCf4 {get;init;}
        internal int? Current1674 {get;init;}
        internal int? CurrentD04 {get;init;}
        internal int? Selector738 {get;init;}
        internal int? Maximum774 {get;init;}
        internal int? Current778 {get;init;}
        internal ulong? Program8008 {get;init;}
    }
    internal sealed record Npc(Binding Current,Binding Template,int NpcId,NpcRaw Raw);
    internal sealed record User(Binding Current,UserRaw Raw);
    internal sealed record Snapshot(Binding Current,ushort? OccupiedWord,uint? Flags1c8,int? Defense1c4,
        uint? Flags214=null,int? Defense210=null);
    internal sealed record ProgramImage(Binding Current,ImmutableArray<byte> Bytes);
    internal sealed record Input(Context Context,Npc Source,User Target,Snapshot? Primary,Snapshot? Backup,
        ProgramImage? Program,int? Telemetry14275eb74,Floating Floating,bool? SerializedNoOtherWriter,bool? NormalReturn,
        NativeEntry Entry=NativeEntry.PhysicalSkill,int? NormalHand=null,string? ModeSourceProof=null);
    internal sealed record Read(int Sequence,ulong NativeSite,string Operand,long Value);
    internal sealed record Result(int BaseDefense,int SnapshotSelector,int SourceSlot190,int DefenseBeforeHp2,
        int MaximumHpRead,int CurrentHpRead,int? SelectedHp2Row,int Hp2Delta,int FinalDefense,uint RatioBits,
        ImmutableArray<Read> Reads,ImmutableDictionary<string,uint> FloatBits)
    {
        internal int? HpResult=>null;
        internal bool ActivationReady=>false;
        internal bool HostEffectsExecuted=>false;
        internal bool CurrentActorObserved=>false;
    }
    internal sealed record Diagnostic(string Code,string Path,string Message,bool UnsupportedProgram);
    internal sealed record Outcome(Result? Value,Diagnostic? Diagnostic)
    {
        [MemberNotNullWhen(true,nameof(Value))]
        internal bool Success=>Value is not null&&Diagnostic is null;
    }
    internal static Outcome Calculate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try{return new(new Calculation(input).Run(),null);}
        catch(ContractError e){return new(null,new(e.Code,e.Path,e.Message,e.Unsupported));}
        catch(ArithmeticException e){return new(null,new("arithmetic","/calculation",e.Message,false));}
    }
    private sealed class Calculation(Input input)
    {
        private readonly List<Read> reads=[];
        private readonly Dictionary<string,uint> words=new(StringComparer.Ordinal);
        private readonly List<(ulong Address,int Bytes,string Role)> spans=[];
        private NpcRaw A=>input.Source.Raw;
        private UserRaw D=>input.Target.Raw;
        private bool Physical=>input.Entry==NativeEntry.PhysicalSkill||
            (input.Entry==NativeEntry.NormalAttack&&A.NormalAttackElement8f4==0);
        private T Read<T>(T? value,string field,ulong site) where T:struct
        {
            T result=Known(value,field);
            reads.Add(new(reads.Count+1,site,field,Convert.ToInt64(result)));
            return result;
        }
        private int Value(int value,string field,ulong site)
        {reads.Add(new(reads.Count+1,site,field,value));return value;}
        private float Keep(string name,float value)
        {Normal(value);words.Add(name,BitConverter.SingleToUInt32Bits(value));return value;}
        internal Result Run()
        {
            Validate();
            int baseDefense=Defense(out int selector);
            int query=Read(D.Selector738,"User+738:slot128",0x1400d1850);
            int selected=Physical
                ?query==0?Read(A.Current1bc8,"Npc+1bc8",0x14036c1d8):query==1?Read(A.Current1bd8,"Npc+1bd8",0x14036c235):0
                :query==0?Read(A.Current1bd0,"Npc+1bd0",0x14036c204):query==1?Read(A.Current1be0,"Npc+1be0",0x14036c256):0;
            int template=Physical?Read(A.Template23c,"sourceTemplate+23c",0x14036c26f):
                Read(A.Template244,"sourceTemplate+244",query==1?0x14036c25dul:0x14036c212ul);
            int sourceDifference=unchecked(selected-template);
            int before=unchecked(baseDefense+sourceDifference);
            // Both caller getter invocations precede the HP2 pointer/null guard.
            int maximum=Read(D.Maximum774,"User+774:slot60",0x1400b2be0);
            int current=Read(D.Current778,"User+778:slot59",0x1400b2bd0);
            int delta=Hp2(current,maximum,before,out int? row);
            int final=unchecked(before+delta);
            var scope=RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
                RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,RetailNpcNormalActorKind.Player,input.Source.NpcId,0);
            float ratio=RetailNpcNormalDamageStages.CalculatePveRatio(scope,0,final);
            Keep("final_ratio",ratio);
            return new(baseDefense,selector,sourceDifference,before,maximum,current,row,delta,final,
                BitConverter.SingleToUInt32Bits(ratio),reads.ToImmutableArray(),words.ToImmutableDictionary(StringComparer.Ordinal));
        }
        private int Defense(out int selector)
        {
            Snapshot primary=SnapshotAt(Read(D.Primary6e50,"User+6e50",Physical?0x1400e00c4ul:0x1400e0b94ul),input.Primary,"primary");
            ushort present=Read(primary.OccupiedWord,"primary+0",Physical?0x1400e00cbul:0x1400e0b9bul);
            if(present!=0)
            {
                uint flags=Physical?Read(primary.Flags1c8,"primary+1c8",0x1400e00d1):Read(primary.Flags214,"primary+214",0x1400e0ba1);
                if((flags&(Physical?0x100000u:0x20000u))!=0)
                {selector=0;return Physical?Read(primary.Defense1c4,"primary+1c4",0x1400e00e3):Read(primary.Defense210,"primary+210",0x1400e0bb3);}
            }
            else
            {
                Snapshot backup=SnapshotAt(Read(D.Backup6e38,"User+6e38",Physical?0x1400e00eeul:0x1400e0bbeul),input.Backup,"backup");
                if(Read(backup.OccupiedWord,"backup+0",Physical?0x1400e00f5ul:0x1400e0bc5ul)!=0)
                {
                    uint flags=Physical?Read(backup.Flags1c8,"backup+1c8",0x1400e00fb):Read(backup.Flags214,"backup+214",0x1400e0bcb);
                    if((flags&(Physical?0x100000u:0x20000u))!=0)
                    {selector=1;return Physical?Read(backup.Defense1c4,"backup+1c4",0x1400e010d):Read(backup.Defense210,"backup+210",0x1400e0bdd);}
                }
            }
            selector=2;
            int initial=Physical?Read(D.Current166c,"User+166c",0x1400e0128):Read(D.Current1674,"User+1674",0x1400e0bf8);
            int bonus=Physical?Read(D.CurrentCf4,"User+cf4:query7a",0x1400b1a23):Read(D.CurrentD04,"User+d04:query7c",0x1400b1a23);
            return unchecked(initial+bonus);
        }
        private Snapshot SnapshotAt(ulong pointer,Snapshot? snapshot,string role)
        {
            Need(pointer!=0&&snapshot is not null&&snapshot.Current.Address==pointer,"snapshot","/"+role,
                "A reached native snapshot pointer must bind its actual record, not a missing-as-zero fallback.");
            Bind(snapshot!.Current,Physical?0x1cc:0x218,role);
            return snapshot;
        }
        private int Hp2(int current,int maximum,int baseDefense,out int? selected)
        {
            selected=null;
            Need(Read(input.Telemetry14275eb74,"global14275eb74",0x140618c86)==0,"telemetry","/telemetry",
                "Active telemetry needs its TLS/clock/observer owners.",true);
            ulong pointer=Read(D.Program8008,"User+8008",0x140618d99);
            int result=0;
            if(pointer!=0)
            {
                ProgramImage? image=input.Program;
                Need(image is not null&&image.Current.Address==pointer&&!image.Bytes.IsDefault&&image.Bytes.Length>=4,
                    "program","/program","Reached program pointer/raw tag is unknown.");
                Bind(image!.Current,image.Bytes.Length,"HP2-program");
                ushort tag=BinaryPrimitives.ReadUInt16LittleEndian(image.Bytes.AsSpan(2,2));
                Value(tag,"HP2+2",0x140618da9);
                if(tag==0x12)result=EvaluateProgram(image.Bytes,current,maximum,baseDefense,out selected);
            }
            else Need(input.Program is null,"program","/program","Known null pointer cannot bind a second selected program.");
            //618c95 creates local telemetry byte54=0. With global14275eb74=0 no
            // path sets it;01e496/49d skip the complete187B release's active child.
            Value(0,"computed local telemetry guard+4:inactive release",0x14001e496);
            return result;
        }
        private int EvaluateProgram(ImmutableArray<byte> bytes,int current,int maximum,int baseDefense,out int? selected)
        {
            Need(bytes.Length==32,"program","/program/raw","The selected HP2 header and all three ordered8B rows require32 bytes.");
            selected=null;
            for(int i=0;i<3;i++)
            {
                ushort attribute=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8+i*8+2,2));
                Value(attribute,"HP2 row"+i+"+2",0x140618e10);
                if(attribute==0x78){selected=i;break;}
            }
            if(selected is null)return 0;
            float hp=Keep("hp_as_float",FromInt(current));
            byte mode=bytes[0];Value(mode,"HP2+0",0x140618e5f);
            if(mode==0)
            {
                Need(maximum>0,"hp-percentage","/program/maximum","Percentage-mode maximumHP must be positive in this finite domain.",true);
                hp=Keep("hp_fraction",Div(hp,FromInt(maximum)));
                hp=Keep("hp_percent",Mul(hp,100f));
            }
            ushort high=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6,2));
            Value(high,"HP2+6",0x140618e74);
            float factor=0f;
            if(hp<=high)
            {
                ushort low=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4,2));
                Value(low,"HP2+4",0x140618e85);
                if(hp<low||high<hp){if(low>hp)factor=1f;}
                else
                {
                    Need(high!=low,"hp-window","/program/window","Exact equal-boundary NaN path needs a separately verified native exceptional-arithmetic policy.",true);
                    float numerator=Keep("window_numerator",Sub(hp,FromInt(low)));
                    float quotient=Keep("window_quotient",Div(numerator,FromInt(high-low)));
                    factor=Sub(1f,quotient);
                    if(factor>1f)factor=1f;
                }
            }
            Keep("factor",factor);
            int offset=8+selected.Value*8;
            ushort rowLow=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset+4,2));
            ushort rowHigh=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset+6,2));
            Value(rowLow,"selected HP2 row+4",0x140618ed2);Value(rowHigh,"selected HP2 row+6",0x140618ed6);
            byte rowMode=bytes[offset];Value(rowMode,"selected HP2 row+0",0x140618edc);
            if(rowMode!=0)
            {
                float product=Keep("absolute_product",Mul(FromInt(rowHigh-rowLow),factor));
                return unchecked(Cvtt(product)+rowLow);
            }
            float amount=Keep("percent_product",Mul(FromInt(rowHigh-rowLow),factor));
            amount=Keep("percent_added",Add(amount,FromInt(rowLow)));
            float nativeBase=FromInt(baseDefense);
            amount=Keep("percent_divided",Div(amount,100f));
            amount=Keep("percent_base_product",Mul(amount,nativeBase));
            return Cvtt(amount);
        }
        private void Validate()
        {
            Need(input.Context is not null&&input.Source is not null&&input.Target is not null&&input.Floating is not null&&
                input.Source.Raw is not null&&input.Target.Raw is not null&&input.Source.Template is not null,
                "input","/context","Required raw actor/template/context objects are absent.");
            Context(input.Context);
            Need(input.Entry is NativeEntry.PhysicalSkill or NativeEntry.MagicalSkill or NativeEntry.NormalAttack,
                "entry","/entry","Unknown native modifier caller.");
            if(input.Entry!=NativeEntry.PhysicalSkill)
                Need(input.ModeSourceProof==ModeSourceProofSha256,"mode-source","/entry/proof",
                    "Normal/magical entry requires the separately verified complete caller/getter bindings.");
            if(input.Entry==NativeEntry.NormalAttack)
                Need(input.NormalHand==0&&A.NormalAttackElement8f4 is >=0 and <=6,"normal-element","/source/8f4",
                    "This entry requires original main hand0 and known current attack element0..6; group/physical flags cannot substitute.",true);
            Need(input.SerializedNoOtherWriter==true&&input.NormalReturn==true,"interval","/context",
                "A serialized normal-return interval with no inline writer is required.");
            Need(input.Floating.NearestEven==true&&input.Floating.ExceptionsMasked==true&&
                input.Floating.FlushToZero==false&&input.Floating.DenormalsAreZero==false,"floating","/environment",
                "Declared RNE/masked/FTZ-DAZ-off contract required; this does not observe MXCSR.");
            Need(A.Type144==2&&A.Vtable==NpcVtable&&A.Kind1940==1&&A.Master188c==0&&A.Related1894==0&&
                A.Virtual1850==0&&A.Difficulty1bf8==0&&input.Source.NpcId>0&&A.Template1848==input.Source.Template.Address,
                "actor","/source","Only an independently bound ordinary ownerless NPC source is admitted.");
            Need(D.Type144==1&&D.Vtable==UserVtable,"actor","/target","Actual User type1/vtable required; an NPC proxy cannot replace this receiver.");
            Bind(input.Source.Current,0x2000,"source NPC");Bind(input.Source.Template,0x500,"source template");
            Bind(input.Target.Current,0x8010,"target User");
            // Supplied aliases must be mutually consistent even when one branch is skipped.
            if(input.Primary is not null&&input.Backup is not null&&input.Primary.Current.Address==input.Backup.Current.Address)
                Need(input.Primary==input.Backup,"alias","/snapshots","One physical snapshot cannot have contradictory binding/raw declarations.");
        }
        private void Context(Context context)
        {
            Need(context is not null&&context.MainSha256==RetailNpcNormalScope.MainBinarySha256&&context.PackageContext==RetailNpcPhysicalInstantParameters.PackageContext&&
                context.ProofSha256==ProofSha256&&context.OracleAmendmentSha256==OracleAmendmentSha256&&context.ReleaseEvidenceSha256==ReleaseEvidenceSha256&&
                context.Origin is Origin.MathematicalFixture or Origin.SourceBoundDeclaration or Origin.ObservedOriginal,
                "evidence","/context","Wrong/unknown Main/package/source/corrected-oracle/release identity.");
            Need(context.Operation==input.Context.Operation&&context.Epoch==input.Context.Epoch&&
                context.Origin==input.Context.Origin&&!string.IsNullOrWhiteSpace(context.Operation)&&
                !string.IsNullOrWhiteSpace(context.Epoch)&&!string.IsNullOrWhiteSpace(context.Receipt),
                "evidence","/context/epoch","Unbound operation/epoch/source receipt.");
        }
        private void Bind(Binding binding,int length,string role)
        {
            Need(binding is not null,"binding","/"+role,"Required current allocation binding is absent.");
            Context(binding.Context);
            Need(binding.Address!=0&&length>0&&binding.Address<=ulong.MaxValue-(ulong)length&&
                !string.IsNullOrWhiteSpace(binding.Generation)&&!string.IsNullOrWhiteSpace(binding.AllocationIdentity),
                "binding","/"+role,"Actual current storage identity/generation/range required.");
            foreach(var old in spans)
            {
                if(old.Address==binding.Address&&old.Role==role)continue;
                // Precisely identical primary/backup record is allowed; all other selected
                // program/actor/template ranges are disjoint in this finite invocation.
                if(old.Address==binding.Address&&old.Bytes==length&&
                    ((old.Role=="primary"&&role=="backup")||(old.Role=="backup"&&role=="primary")))continue;
                Need(binding.Address+(ulong)length<=old.Address||old.Address+(ulong)old.Bytes<=binding.Address,
                    "alias","/"+role,"Unsupported overlap among selected raw actor/template/snapshot/program storage.");
            }
            spans.Add((binding.Address,length,role));
        }
    }
    private static T Known<T>(T? value,string field) where T:struct
    {Need(value.HasValue,"unknown","/raw/"+field,"Missing reached raw operand.");return value.GetValueOrDefault();}
    private static void Need([DoesNotReturnIf(false)]bool value,string code,string path,string message,bool unsupported=false)
    {if(!value)throw new ContractError(code,path,message,unsupported);}
    private sealed class ContractError(string code,string path,string message,bool unsupported):Exception(message)
    {internal string Code=>code;internal string Path=>path;internal bool Unsupported=>unsupported;}
    private static float Normal(float value)
    {
        uint magnitude=BitConverter.SingleToUInt32Bits(value)&0x7fffffff;
        Need(magnitude<0x7f800000&&(magnitude==0||magnitude>=0x00800000),"floating","/intermediate","Consumed nonfinite/subnormal value is outside this finite domain.");
        return value;
    }
    private static int Cvtt(float value)
    {Normal(value);Need(value>=-2147483648f&&value<2147483648f,"conversion","/intermediate","Invalid signed32 native conversion.");return(int)value;}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float FromInt(int value)=>Normal(value);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>Normal(a+b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>Normal(a-b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>Normal(a*b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>Normal(a/b);
}
