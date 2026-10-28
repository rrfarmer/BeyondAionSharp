using System.Globalization;
using System.Text.Json;
using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.NpcAudit;

internal static partial class Program
{
    static partial void AdditionalCommand(string[] args, ref int? result)
    {
            if(args.Length==10 && args[0]=="--armor-enchant-scenario")
            {
                int[] value=args[1..].Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                int retail=RetailNpcArmorEnchantMath.Retail(value[0],value[1],value[2],value[3],value[4]);
                int host=RetailNpcArmorEnchantMath.Host(value[0],value[5],value[6],value[7],value[8]);
                Console.Write(JsonSerializer.Serialize(new {context="explicit-selected-armor-enchant-pdef-inputs",
                    runtime_enabled=false,level=value[0],retail_pdef=retail,host_pdef=host,difference=(long)retail-host,
                    limitation="Table contribution only; no actor, equip eligibility, level penalty, item bonus or final defense."},Json));
                result = 0; return;
            }
            if(args.Length==10 && args[0]=="--armor-hp-enchant-scenario")
            {
                int[] value=args[1..].Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                int retail=RetailNpcArmorEnchantMath.Retail(value[0],value[1],value[2],value[3],value[4]);
                int host=RetailNpcArmorEnchantMath.Host(value[0],value[5],value[6],value[7],value[8]);
                Console.Write(JsonSerializer.Serialize(new {context="explicit-selected-armor-enchant-max-hp-inputs",
                    runtime_enabled=false,level=value[0],retail_max_hp=retail,host_max_hp=host,difference=(long)retail-host,
                    limitation="Enchant contribution only; no actor, equip eligibility, item level penalty, bonus limits or final HP."},Json));
                result = 0; return;
            }
            if (args.Length == 8 && args[0] == "--curse-scenario")
            {
                var resource = args[1] switch { "hp" => RetailNpcCurseStatMath.Resource.Hp, "mp" => RetailNpcCurseStatMath.Resource.Mp,
                    _ => throw new ArgumentException("Resource must be hp or mp") };
                int[] value = args[2..].Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                int totalPercent = checked(value[2] + value[3]);
                int maximum = RetailNpcCurseStatMath.Maximum(resource, value[0], value[1], totalPercent);
                int current = RetailNpcCurseStatMath.CurrentMode2(resource, value[4], maximum, value[5]);
                Console.Write(JsonSerializer.Serialize(new { context = "explicit-native-stage-inputs-mode2-suppression-clear",
                    runtime_enabled = false, resource = args[1], percentage_base = value[0], unscaled_total = value[1],
                    existing_percent = value[2], added_percent = value[3], aggregate_percent = totalPercent,
                    old_maximum = value[4], old_current = value[5], new_maximum = maximum, new_current = current,
                    limitation = "Illustrative inputs; not proof of a 5.8 player, equipment, source binding or active effect." }, Json));
                result = 0; return;
            }
            if (args.Length == 3 && args[0] == "--curse-class-bases")
            {
                int level=int.Parse(args[2],CultureInfo.InvariantCulture);
                if(level is <1 or >65)throw new ArgumentOutOfRangeException(nameof(level),"Select a 4.8 level from 1 to 65.");
                PlayerClass[] classes;
                if(args[1]=="all")classes=Enum.GetValues<PlayerClass>();
                else if(Enum.TryParse<PlayerClass>(args[1],true,out var chosen) && Enum.IsDefined(chosen)
                    && string.Equals(chosen.ToString(),args[1],StringComparison.OrdinalIgnoreCase))classes=[chosen];
                else throw new ArgumentException("Choose a named 4.8 player class or all.");
                var classRows=classes.Select(pc=>
                {
                    var stats=pc.CreateStatsTemplate(level);
                    int health=stats.GetHealth(),will=stats.GetWill(),nativeClass=pc.GetClassId();
                    int hp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,(ushort)level,(ushort)health,nativeClass);
                    int mp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,(ushort)level,(ushort)will,nativeClass);
                    var retailPrimary=RetailNpcCurseStatMath.InitialClassPrimary(nativeClass);
                    int retailClassHp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,(ushort)level,retailPrimary.Health,nativeClass);
                    int retailClassMp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,(ushort)level,retailPrimary.Will,nativeClass);
                    return new {player_class=pc.ToString(),class_id=nativeClass,level,host_health=health,host_will=will,
                        host_base_hp=stats.GetMaxHp(),host_base_mp=stats.GetMaxMp(),retail_initial_hp_percentage_base=hp,
                        retail_initial_mp_percentage_base=mp,retail_curse_hp_component=(long)hp * -35 / 100,
                        retail_curse_mp_component=(long)mp * -35 / 100,
                        retail_class_initial_health=retailPrimary.Health,retail_class_initial_will=retailPrimary.Will,
                        retail_class_initial_hp_percentage_base=retailClassHp,retail_class_initial_mp_percentage_base=retailClassMp,
                        retail_class_curse_hp_component=(long)retailClassHp * -35 / 100,
                        retail_class_curse_mp_component=(long)retailClassMp * -35 / 100};
                }).ToArray();
                Console.Write(JsonSerializer.Serialize(new {context="host-class-primary-inputs-retail-initial-percentage-bases",
                    runtime_enabled=false,rows=classRows,
                    curse_component_assumption="Zero existing percentage modifiers; native arithmetic combines all percentages before truncation.",
                    limitation="Original fields use 4.8 class HEALTH/WILL; retail_class fields use 5.8 initial class/race values. These are not final player maxima; gear, effects, native update modes and live state are not modeled."},Json));
                result = 0; return;
            }
    }
}
