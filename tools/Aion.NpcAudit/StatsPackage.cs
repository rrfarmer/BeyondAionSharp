using System.Globalization;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Retail;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.NpcAudit;

internal static partial class Program
{
    private static int StatsPackage(string manifest,string pin,string templates,string output)
    {
        if(RetailNpcStatCatalog.Enabled)
            throw new InvalidOperationException("Offline package audit requires inactive gameplay catalogs");
        var directory=Path.GetFullPath(output);
        var repo=new DirectoryInfo(AppContext.BaseDirectory);
        while(repo is not null&&!File.Exists(Path.Combine(repo.FullName,"AionServer.slnx")))repo=repo.Parent;
        if(repo is null||Path.GetRelativePath(repo.FullName,directory) is var relative
            &&!Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))
            throw new InvalidDataException("Audit output must be outside the checkout");
        if(!Directory.Exists(directory)||Directory.EnumerateFiles(directory,"profiles.*").Any())
            throw new InvalidDataException("Use a new output directory through npc_audit.py stats-package");
        byte[] manifestBytes=File.ReadAllBytes(manifest);string templatePin=Hash(File.ReadAllBytes(templates));
        using var manifestDocument=JsonDocument.Parse(manifestBytes);
        string? spellConditionContext=manifestDocument.RootElement.TryGetProperty("spell_condition_context",out var conditionAsset)
            ?conditionAsset.GetProperty("sha256").GetString():null;
        var snapshot=RetailNpcStatCatalog.ReadStatPackageForAudit(manifestBytes,pin,Path.GetDirectoryName(Path.GetFullPath(manifest))!);
        var legacy=Load48(templates,snapshot.Members.Select(x=>x.Profile.NpcId).ToHashSet());
        var rows=new List<object>();var counts=new Dictionary<string,int>(StringComparer.Ordinal);
        var bindings=snapshot.SkillBindings.ToLookup(x=>(x.MapId,x.NpcId));
        var changed=new List<(int Map,int Npc,string Field,object? Old,int New)>();
        foreach(var member in snapshot.Members)
        {
            var p=member.Profile;
            if(!legacy.TryGetValue(p.NpcId,out var host)||RetailNpcHostIdentity.Template(host)!=member.HostTemplateSha256)
                throw new InvalidDataException("Pinned host identity differs: "+p.NpcId);
            var old=Baseline(host);
            // Ordinary physical legacy branch, excluding AI hooks/effects/status/target.
            // Use the actual host Stat2/cap and sample arithmetic, not raw retail attack.
            var attackStat=new AdditionStat(StatEnum.PHYSICAL_ATTACK,host.GetStatsTemplate().GetAttack(),null!);
            StatCapUtil.CalculateBaseValue(attackStat,null!);
            var normal48=new {context="initialized-template-before-effects-ai-status-target",element="physical",
                current_attack=attackStat.GetCurrent(),
                minimum=StatFunctions.CalculateNpcNormalSample(attackStat.GetCurrent(),80),
                maximum=StatFunctions.CalculateNpcNormalSample(attackStat.GetCurrent(),120)};
            bool dual=p.Source.Integers["dual"].ParsedValue is not (null or 0);
            var split=dual?RetailNpcDualNormalPlan.Split(p.MinDamage,p.MaxDamage,p.HitCount):null;
            object Range(int first,int second,int modifier)=>new {
                minimum=Math.Max(1f,RetailNpcStatNormalInputs.CalculateBase(Math.Min(first,second),modifier,new(0,0))),
                maximum=Math.Max(1f,RetailNpcStatNormalInputs.CalculateBase(Math.Max(first,second),modifier,new(0,0)))};
            var normal58=p.Attacks.Select(a=>new {a.Ordinal,a.Type,a.Weight,a.Modifier,
                main=Range(split?.Main.Minimum??p.MinDamage,split?.Main.Maximum??p.MaxDamage,a.Modifier),
                off=split is null?null:Range(split.Offhand.Minimum,split.Offhand.Maximum,a.Modifier)}).ToArray();
            // No movement, range, cadence, regeneration or actor context is applied.
            var fields=p.Values.Where(x=>x.Key!="hp_regen").OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>
            {
                old.TryGetValue(x.Key,out var before);
                bool comparable=before is not null;
                bool equal=comparable&&Convert.ToInt64(before,CultureInfo.InvariantCulture)==x.Value;
                string status=!comparable?"baseline_unavailable":equal?"matches":"differs";
                counts[status]=counts.GetValueOrDefault(status)+1;
                if(comparable&&!equal)changed.Add((member.MapId,p.NpcId,x.Key,before,x.Value));
                return new {field=x.Key,baseline48=before,reference58=x.Value,status,
                    source_presence=p.ScalarInputs.Fields[x.Key].SourcePresence.ToString()};
            }).ToArray();
            rows.Add(new {map_id=member.MapId,npc_id=p.NpcId,name48=host.GetName(),retail_devname=p.Devname,
                host_template_sha256=member.HostTemplateSha256,source=p.Source.Receipt,fields,
                normal_min_damage=p.MinDamage,normal_max_damage=p.MaxDamage,
                normal_attack_correction_supported=p.HasNormalAttackCorrection,ordered_attacks=p.Attacks,
                normal_before_target48=normal48,normal_before_target58=normal58,
                unresolved_source_skill_bindings=p.Source.Source.SkillBindings.EnumerateArray()
                    .Where(s=>s.GetProperty("status").GetString()!="dual_join_agrees_static_only").ToArray(),
                declared_skills=bindings[(member.MapId,p.NpcId)].Select(s=>new {skill_id=s.SkillId,level=s.Level,
                    source_bound=s.SourceBound,primary_supported=(bool?)null,
                    primary_boundary="not-assessed-by-mob-stat-audit"}).ToArray()});
        }
        if(Hash(File.ReadAllBytes(templates))!=templatePin||Hash(File.ReadAllBytes(manifest))!=pin)
            throw new InvalidDataException("Audit input changed during calculation");
        if(RetailNpcStatCatalog.Enabled)
            throw new InvalidOperationException("Audit activated gameplay");
        var report=new {version=1,context="fresh-unowned-ordinary-kind1",offline_only=true,activation_ready=false,
            scope="Only selected package memberships. Base HP/attributes/defenses and normal pre-effect/pre-target damage; no final HP loss, current effects, owned/difficulty scaling or behavior certification.",
            manifest=Path.GetFullPath(manifest),manifest_sha256=pin,templates48=Path.GetFullPath(templates),templates48_sha256=templatePin,
            spell_condition_context_sha256=spellConditionContext,
            calculator_sha256=Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
            game_server_assembly_sha256=Hash(File.ReadAllBytes(typeof(RetailNpcStatCatalog).Assembly.Location)),
            retail_main_sha256=Aion.GameServer.Dataholders.RetailNpc.RetailNpcTemplateProjection.MainBinarySha256,
            summary=new {memberships=rows.Count,unique_npcs=legacy.Count,selected_maps=snapshot.Maps.Length,field_statuses=counts},
            supported_maps=snapshot.Maps,rows,primary_skill_audit="not-performed-outside-mob-stat-scope",
            buff_sources=snapshot.BuffSources};
        using(var stream=new FileStream(Path.Combine(directory,"profiles.json"),FileMode.CreateNew,FileAccess.Write))
            JsonSerializer.Serialize(stream,report,Json);
        static string Csv(object? value)=>"\""+(Convert.ToString(value,CultureInfo.InvariantCulture)??"").Replace("\"","\"\"")+"\"";
        using(var writer=new StreamWriter(new FileStream(Path.Combine(directory,"profiles.csv"),FileMode.CreateNew,FileAccess.Write),new UTF8Encoding(false)))
        {
            writer.WriteLine("map_id,npc_id,field,baseline48,reference58");
            foreach(var c in changed)writer.WriteLine(string.Join(",",new object?[]{c.Map,c.Npc,c.Field,c.Old,c.New}.Select(Csv)));
        }
        using(var stream=new FileStream(Path.Combine(directory,"calculation-receipt.json"),FileMode.CreateNew,FileAccess.Write))
            JsonSerializer.Serialize(stream,new {offline_only=true,activation_ready=false,manifest_sha256=pin,
                templates48_sha256=templatePin,profiles_sha256=Hash(File.ReadAllBytes(Path.Combine(directory,"profiles.json"))),
                profiles_csv_sha256=Hash(File.ReadAllBytes(Path.Combine(directory,"profiles.csv"))),
                calculator_sha256=report.calculator_sha256,game_server_assembly_sha256=report.game_server_assembly_sha256},Json);
        Console.Write(JsonSerializer.Serialize(report.summary,Json));
        return 0;
    }
}
