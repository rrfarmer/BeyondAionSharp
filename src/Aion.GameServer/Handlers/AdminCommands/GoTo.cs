using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;
using static Aion.GameServer.World.WorldMapType;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/GoTo (Dwarfpicker, Imaginary, Neon). LinkedHashMap -> insertion-ordered Dictionary; WordUtils.capitalizeFully inlined.</summary>
public class GoTo : AdminCommand
{
    private readonly Dictionary<string, Location> locations = new Dictionary<string, Location>();

    public GoTo()
        : base("goto", "Teleports you to regions by name.")
    {
        SetSyntaxInfo(
            " - Shows a list of locations to teleport to.",
            "<location name> - Teleports you to the given location.");
        AddLocations();
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            ListLocations(player);
            return;
        }

        string destination = string.Join(" ", paramsArr).Trim().ToLower();
        locations.TryGetValue(destination, out Location location);
        if (location == null)
        {
            Dictionary<string, Location> matches = FindPossibleMatch(destination);
            if (IsSingleSureMatch(matches, destination))
            {
                location = matches.Values.First();
            }
            else
            {
                System.Text.StringBuilder msg = new System.Text.StringBuilder("Could not find the specified destination.");
                if (matches.Count != 0)
                {
                    msg.Append(" Possible matches:");
                    foreach (KeyValuePair<string, Location> e in matches)
                        msg.Append("\n\t- ").Append(ChatUtil.Color(e.Key, Color.White)).Append(" (").Append(GetName(e.Value.mapType)).Append(")");
                }
                SendInfo(player, msg.ToString());
            }
            if (location == null)
                return;
        }

        DoGoTo(player, location);
    }

    private bool IsSingleSureMatch(Dictionary<string, Location> matches, string query)
    {
        return matches.Count == 1 && (query.Length >= 5 || matches.Keys.First().ToLower().StartsWith(query));
    }

    private Dictionary<string, Location> FindPossibleMatch(string destination)
    {
        Dictionary<string, Location> possibleMatches = new Dictionary<string, Location>();
        if (destination.Length >= 2)
        {
            foreach (Location loc in locations.Values)
            {
                foreach (string identifier in loc.identifiers)
                {
                    if (identifier.ToLower().Contains(destination))
                    {
                        possibleMatches[identifier] = loc;
                        break;
                    }
                }
            }
        }
        return possibleMatches;
    }

    private static void DoGoTo(Player player, Location location)
    {
        WorldMapInstance instance;
        if (player.GetWorldId() == location.mapType.GetId())
            instance = player.GetPosition().GetWorldMapInstance();
        else if (World.World.GetInstance().GetWorldMap(location.mapType.GetId()).IsInstanceType())
            instance = InstanceService.GetOrRegisterInstance(location.mapType.GetId(), player);
        else
            instance = World.World.GetInstance().GetWorldMap(location.mapType.GetId()).GetMainWorldMapInstance();
        TeleportService.TeleportTo(player, instance, location.x, location.y, location.z, location.h != 0 ? location.h : player.GetHeading(),
            TeleportAnimation.NONE);
    }

    private void ListLocations(Player player)
    {
        Dictionary<WorldMapType, List<Location>> locsByWorld = new Dictionary<WorldMapType, List<Location>>();
        foreach (KeyValuePair<string, Location> e in locations)
        {
            if (!locsByWorld.TryGetValue(e.Value.mapType, out List<Location> list))
            {
                list = new List<Location>();
                locsByWorld[e.Value.mapType] = list;
            }
            if (!list.Contains(e.Value))
                list.Add(e.Value);
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (locsByWorld.Count == 1)
            sb.Append("Locations for ");
        else
            sb.Append("List of locations per map:\n");
        foreach (KeyValuePair<WorldMapType, List<Location>> entry in locsByWorld)
            AppendLocationsForMap(sb, entry.Key, entry.Value);
        SendInfo(player, sb.ToString());
    }

    private void AppendLocationsForMap(System.Text.StringBuilder sb, WorldMapType worldMapType, ICollection<Location> locs)
    {
        sb.Append(GetName(worldMapType)).Append(':');
        if (locs.Count > 1)
            sb.Append('\n');
        foreach (Location loc in locs)
        {
            sb.Append('\t');
            if (locs.Count > 1)
                sb.Append("- ");
            AppendLocNames(sb, loc.identifiers);
            sb.Append('\n');
        }
    }

    private string GetName(WorldMapType worldMapType)
    {
        return CapitalizeFully(worldMapType.ToString().Replace('_', ' '));
    }

    // Java parity: org.apache.commons.lang3.text.WordUtils.capitalizeFully(String) -
    // lowercases the whole string, then capitalizes the first letter of each whitespace-delimited word.
    private static string CapitalizeFully(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        str = str.ToLower();
        char[] buffer = str.ToCharArray();
        bool capitalizeNext = true;
        for (int i = 0; i < buffer.Length; i++)
        {
            char ch = buffer[i];
            if (char.IsWhiteSpace(ch))
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                buffer[i] = char.ToUpper(ch);
                capitalizeNext = false;
            }
        }
        return new string(buffer);
    }

    private void AppendLocNames(System.Text.StringBuilder sb, List<string> locNames)
    {
        for (int i = 0; i < locNames.Count;)
        {
            sb.Append(ChatUtil.Color(locNames[i++], Color.White));
            if (i != locNames.Count)
                sb.Append(" // ");
        }
    }

    private void AddLocation(WorldMapType mapType, float x, float y, float z, params string[] identifiers)
    {
        AddLocation(mapType, x, y, z, (byte)0, identifiers);
    }

    private void AddLocation(WorldMapType mapType, float x, float y, float z, byte h, params string[] identifiers)
    {
        Location location = new Location(mapType, x, y, z, h, identifiers.ToList());
        foreach (string identifier in identifiers)
        {
            string key = identifier.ToLower();
            if (locations.ContainsKey(key))
                throw new ArgumentException("Duplicate location identifier: " + identifier);
            locations[key] = location;
        }
    }

    private sealed class Location
    {
        internal readonly WorldMapType mapType;
        internal readonly float x, y, z;
        internal readonly byte h;
        internal readonly List<string> identifiers;

        public Location(WorldMapType mapType, float x, float y, float z, byte h, List<string> identifiers)
        {
            this.mapType = mapType;
            this.x = x;
            this.y = y;
            this.z = z;
            this.h = h;
            this.identifiers = identifiers;
        }
    }

    private void AddLocations()
    {
        /*
         * Elysea
         */
        AddLocation(SANCTUM, 1322, 1511, 568, "Sanctum");
        AddLocation(KAISINEL, 2155, 1567, 1205, "Kaisinel", "Kaisinels Kloster");
        AddLocation(WISPLIGHT_ABBEY, 247, 228, 130, "Wisplight Abbey", "Zuflucht der Rückkehrer Elyos", "Zuflucht Elyos");
        AddLocation(POETA, 806, 1242, 119, "Poeta");
        AddLocation(POETA, 426, 1740, 119, "Melponeh", "Melponehs Lager");
        AddLocation(VERTERON, 1643, 1500, 119, "Verteron");
        AddLocation(VERTERON, 2384, 788, 102, "Cantas Coast", "Kantas Küste");
        AddLocation(VERTERON, 2333, 1817, 193, "Ardus Shrine", "Ardus Schrein");
        AddLocation(VERTERON, 2063, 2412, 274, "Pilgrims Respite", "Pilgers Rast");
        AddLocation(VERTERON, 1291, 2206, 142, "Tolbas Village");
        AddLocation(ELTNEN, 343, 2724, 264, "Eltnen");
        AddLocation(ELTNEN, 688, 431, 332, "Golden", "Golden Bough Garrison", "Garnison der Goldbogenlegion");
        AddLocation(ELTNEN, 1779, 883, 422, "Eltnen Observatory", "Observatorium von Eltnen");
        AddLocation(ELTNEN, 947, 2215, 252, "Novan", "Novans Kreuzung");
        AddLocation(ELTNEN, 1921, 2045, 361, "Agairon");
        AddLocation(ELTNEN, 2411, 2724, 361, "Kuriullu");
        AddLocation(THEOBOMOS, 1398, 1557, 31, "Theobomos");
        AddLocation(THEOBOMOS, 458, 1257, 127, "Jamanok Inn", "Jamanoks Gasthaus");
        AddLocation(THEOBOMOS, 1396, 1560, 31, "Meniherk");
        AddLocation(THEOBOMOS, 2234, 2284, 50, "Obsvillage", "Observatoriumsdorf");
        AddLocation(THEOBOMOS, 901, 2774, 62, "Josnack", "Josnacks Zelt");
        AddLocation(THEOBOMOS, 2681, 847, 138, "Anangke");
        AddLocation(HEIRON, 2540, 343, 411, "Heiron");
        AddLocation(HEIRON, 1423, 1334, 175, "Heiron Observatory", "Heiron Observatorium");
        AddLocation(HEIRON, 971, 686, 135, "Senemonea", "Seneas Lager");
        AddLocation(HEIRON, 1635, 2693, 115, "Jeiaparan");
        AddLocation(HEIRON, 916, 2256, 157, "Changarnerk", "Changarnerks Lager");
        AddLocation(HEIRON, 1999, 1391, 118, "Kishar");
        AddLocation(HEIRON, 170, 1662, 120, "Arbolu", "Arbolus Oase");

        /*
         * Asmodae
         */
        AddLocation(PANDAEMONIUM, 1679, 1400, 195, "Pandaemonium", "Pandämonium");
        AddLocation(MARCHUTAN, 1557, 1429, 266, "Marchutan", "Marchutans Konvent");
        AddLocation(FATEBOUND_ABBEY, 281, 266, 97, "Fatebound Abbey", "Zuflucht der Rückkehrer Asmo", "Zuflucht Asmo");
        AddLocation(ISHALGEN, 529, 2449, 281, "Ishalgen");
        AddLocation(ISHALGEN, 940, 1707, 259, "Anturoon", "Anturoon Wachposten");
        AddLocation(ALTGARD, 1748, 1807, 254, "Altgard");
        AddLocation(ALTGARD, 1903, 696, 260, "Basfelt");
        AddLocation(ALTGARD, 2680, 1024, 311, "Trader", "Handelshafen");
        AddLocation(ALTGARD, 2643, 1658, 324, "Impetusiom", "Herz des Impetusiums");
        AddLocation(ALTGARD, 1468, 2560, 299, "Altgard Observatory", "Observatorium von Altgard");
        AddLocation(MORHEIM, 308, 2274, 449, "Morheim");
        AddLocation(MORHEIM, 634, 900, 360, "Desert", "Wüstengarnison");
        AddLocation(MORHEIM, 1772, 1662, 197, "Slag", "Schlackenbollwerk");
        AddLocation(MORHEIM, 1070, 2486, 239, "Kellan", "Kellans Hütte");
        AddLocation(MORHEIM, 2387, 1742, 102, "Alsig");
        AddLocation(MORHEIM, 2794, 1122, 171, "Morheim Observatory", "Observatorium von Morheim");
        AddLocation(MORHEIM, 2346, 2219, 127, "Halabana");
        AddLocation(BRUSTHONIN, 2917, 2421, 15, "Brusthonin");
        AddLocation(BRUSTHONIN, 1413, 2013, 51, "Baltasar");
        AddLocation(BRUSTHONIN, 840, 2016, 307, "Bollu", "Iollu");
        AddLocation(BRUSTHONIN, 1523, 374, 231, "Edge", "Saum der Qualen");
        AddLocation(BRUSTHONIN, 526, 848, 76, "Bubu", "Bubu Dorf");
        AddLocation(BRUSTHONIN, 2917, 2417, 15, "Settlers", "Siedlerlager");
        AddLocation(BELUSLAN, 398, 400, 222, "Beluslan");
        AddLocation(BELUSLAN, 533, 1866, 262, "Besfer");
        AddLocation(BELUSLAN, 1243, 819, 260, "Kidorun", "Kidoruns Lager");
        AddLocation(BELUSLAN, 2358, 1241, 470, "Red Mane", "Rotmähnenhöhle");
        AddLocation(BELUSLAN, 1942, 513, 412, "Kistenian");
        AddLocation(BELUSLAN, 2431, 2063, 579, "Hoarfrost", "Raureif");

        /*
         * Balaurea
         */
        AddLocation(INGGISON, 1335, 276, 590, "Inggison");
        AddLocation(INGGISON, 382, 951, 460, "Ufob", "Undirborg");
        AddLocation(INGGISON, 2713, 1477, 382, "Soteria");
        AddLocation(INGGISON, 1892, 1748, 327, "Hanarkand");
        AddLocation(GELKMAROS, 1763, 2911, 554, "Gelkmaros");
        AddLocation(GELKMAROS, 2503, 2147, 464, "Subterranea");
        AddLocation(GELKMAROS, 845, 1737, 354, "Rhonnam");
        AddLocation(GELKMAROS, 1295, 1558, 306, "Späherposten");
        AddLocation(SILENTERA_CANYON, 583, 767, 300, "Silentera");

        /*
         * Abyss
         */
        AddLocation(RESHANTA, 951, 936, 1667, "Reshanta");
        AddLocation(RESHANTA, 2867, 1034, 1528, "Abyss 1");
        AddLocation(RESHANTA, 1078, 2839, 1636, "Abyss 2");
        AddLocation(RESHANTA, 1596, 2952, 2943, "Abyss 3");
        AddLocation(RESHANTA, 2054, 660, 2843, "Abyss 4");
        AddLocation(RESHANTA, 1979, 2114, 2291, "Eye of Reshanta");
        AddLocation(RESHANTA, 2130, 1925, 2322, "Divine Fortress");

        /*
         * Instances
         */
        AddLocation(HARAMEL, 176, 21, 144, "Haramel");
        AddLocation(NOCHSANA_TRAINING_CAMP, 513, 668, 331, "Nochsana Training Camp", "NTC");
        AddLocation(ARKANIS_TEMPLE, 177, 229, 536, "Sky Temple of Arkanis", "Himmelstempel von Arkanis");
        AddLocation(FIRE_TEMPLE, 144, 312, 123, "Fire Temple", "FT", "Feuertempel");
        AddLocation(KROMEDES_TRIAL, 248, 244, 189, "Kromedes Trial", "Kromedes Prozess");
        AddLocation(STEEL_RAKE, 237, 506, 948, "Steel Rake", "SR", "Stahlharke");
        AddLocation(STEEL_RAKE, 283, 453, 903, "Steel Rake Lower", "SR Low", "Stahlharke Unten");
        AddLocation(STEEL_RAKE, 283, 453, 953, "Steel Rake Middle", "SR Mid", "Stahlharke Mitte");
        AddLocation(INDRATU_FORTRESS, 562, 335, 1015, "Indratu Fortress", "Indratu Festung");
        AddLocation(AZOTURAN_FORTRESS, 458, 428, 1039, "Azoturan Fortress", "Azoturan Festung");
        AddLocation(AETHEROGENETICS_LAB, 225, 244, 133, "Aetherogenetics Lab", "Lepharisten Geheimlabor");
        AddLocation(ADMA_STRONGHOLD, 450, 200, 168, "Adma", "Adma Stronghold", "Adma Festung");
        AddLocation(ALQUIMIA_RESEARCH_CENTER, 603, 527, 200, "Alquimia Research Center", "Alquimia Labor");
        AddLocation(DRAUPNIR_CAVE, 491, 373, 622, "Draupnir Cave", "Draupnir Höhle");
        AddLocation(THEOBOMOS_LAB, 477, 201, 170, "Theobomos Lab", "Theobomos Research Lab", "TheoLab", "Theobomos Geheimlabor");
        AddLocation(DARK_POETA, 1214, 412, 140, "Dark Poeta", "DP", "Poeta der Finsternis");
        AddLocation(SHUGO_IMPERIAL_TOMB, 178, 234, 537, "Shugo Imperial Tomb", "Imperial", "Tomb");
        // Lower Abyss
        AddLocation(SULFUR_TREE_NEST, 462, 345, 163, "Sulfur Tree Nest", "Schwefelbaum Nest");
        AddLocation(RIGHT_WING_CHAMBER, 263, 386, 103, "Right Wing Chamber", "Rechter Flügel", "Kammer Im Rechten Flügel");
        AddLocation(LEFT_WING_CHAMBER, 672, 606, 321, "Left Wing Chamber", "Linker Flügel", "Kammer Im Linken Flügel");
        // Upper Abyss
        AddLocation(ASTERIA_CHAMBER, 469, 568, 202, "Asteria Chamber", "Abyss von Asteria");
        AddLocation(MIREN_CHAMBER, 527, 120, 176, "Miren Chamber", "Miren Kammer");
        AddLocation(LEGIONS_MIREN_BARRACKS, 528, 121, 176, "Miren Legion Barracks", "Miren Legionsfestung");
        AddLocation(MIREN_BARRACKS, 528, 121, 176, "Miren Barracks", "Miren Kriegsfestung");
        AddLocation(KYSIS_CHAMBER, 528, 121, 176, "Kysis Chamber", "Kysis Kammer");
        AddLocation(LEGIONS_KYSIS_BARRACKS, 528, 121, 176, "Kysis Legion Barracks", "Kysis Legionsfestung");
        AddLocation(KYSIS_BARRACKS, 528, 121, 176, "Kysis Barracks", "Kysis Kriegsfestung");
        AddLocation(KROTAN_CHAMBER, 528, 109, 176, "Krotan Chamber", "Krotan Kammer");
        AddLocation(LEGIONS_KROTAN_BARRACKS, 528, 121, 176, "Krotan Legion Barracks", "Krotan Legionsfestung");
        AddLocation(KROTAN_BARRACKS, 528, 121, 176, "Krotan Barracks", "Krotan Kriegsfestung");
        AddLocation(CHAMBER_OF_ROAH, 504, 396, 94, "Roah Chamber", "Kaverne von Roah");
        // Divine
        AddLocation(ABYSSAL_SPLINTER, 704, 153, 453, "Abyssal Splinter", "AS", "Abyss Splitter");
        AddLocation(UNSTABLE_SPLINTER, 704, 153, 453, "Unstable Abyssal Splinter", "UAS", "Zerbrochener Abyss Splitter");
        AddLocation(DREDGION, 414, 193, 431, "Dredgion");
        AddLocation(DREDGION_OF_CHANTRA, 414, 193, 431, "Chantra Dredgion");
        AddLocation(TERATH_DREDGION, 414, 193, 431, "Terath Dredgion", "Sadha Dredgion");
        AddLocation(TALOCS_HOLLOW, 200, 214, 1099, "Taloc's Hollow", "Talocs Höhle");
        AddLocation(UDAS_TEMPLE, 637, 657, 134, "Udas", "Udas Temple", "Udas Tempel");
        AddLocation(UDAS_TEMPLE_LOWER, 1146, 277, 116, "Udas Lower Temple", "Udas Tempelgruft");
        AddLocation(BESHMUNDIR_TEMPLE, 1477, 237, 243, "Beshmundir Temple", "BT");
        AddLocation(PADMARASHKA_CAVE, 385, 506, 66, "Padmarashka Cave");
        // 3.0 Instances
        AddLocation(ATURAM_SKY_FORTRESS, 691.459f, 456.8719f, 655.7797f, "ASF", "Aturam Sky Fortress", "Aturam Himmelsfestung");
        // 4.0 Instances
        AddLocation(SAURO_SUPPLY_BASE, 640.7884f, 174.29156f, 195.625f, "Sauro Supply Base");
        AddLocation(ETERNAL_BASTION, 745.86206f, 291.18323f, 233.7940f, "Eternal Bastion", "EB", "Stahlmauerbastion");
        AddLocation(SEALED_DANUAR_MYSTICARIUM, 184.35f, 121.3f, 231.3f, "Danuar Mysticarium", "DM", "Versiegelte Halle des Wissens", "VHDW");
        AddLocation(OPHIDAN_BRIDGE, 755.41864f, 560.617f, 572.9637f, "Ophidan Bridge", "OB", "Jormungand Brücke");
        AddLocation(DANUAR_RELIQUARY, 256.60f, 257.99f, 241.78f, "Danuar Reliquary", "DR", "Ruhnadium");
        AddLocation(DANUAR_SANCTUARY, 388.66f, 1185.07f, 55.31f, "Danuar Sanctuary", "DS", "Zuflucht der Ruhn", "ZDR");
        AddLocation(SEIZED_DANUAR_SANCTUARY, 388.66f, 1185.07f, 55.31f, "Seized Danuar Sanctuary", "SDS", "Verlorene Zukunft", "VZ");
        AddLocation(THE_NIGHTMARE_CIRCUS, 467.64f, 568.34f, 201.67f, "Nightmare Circus", "NC", "Rukibuki Zirkus");
        AddLocation(KAMAR_BATTLEFIELD, 1329, 1501, 593, "Kamar Battlefield", "KB", "Schlachtfeld von Kamar");
        // 4.3 Instance
        AddLocation(HEXWAY, 672.8f, 606.2f, 321.2f, "The Hexway", "Korridor des Verrats", "KDV");
        // 4.5 instances
        AddLocation(ENGULFED_OPHIDAN_BRIDGE, 753, 532, 577, "Engulfed Ophidian Bridge", "EOB", "Jormungand Marschroute");
        AddLocation(IRON_WALL_WARFRONT, 550, 477, 213, "Iron Wall Warfront", "IWW", "Schlachtfeld der Stahlmauerbastion");
        AddLocation(LUCKY_OPHIDAN_BRIDGE, 750, 554, 574, "Lucky Ophidian Bridge", "LOB", "Lucky Ophidan", "Jormungand Bonus");
        AddLocation(LUCKY_DANUAR_RELIQUARY, 750, 554, 574, "Lucky Danuar Reliquary", "LDR", "Lucky Danuar");
        AddLocation(ILLUMINARY_OBELISK, 322.38f, 324.47f, 405.49997f, "Illuminary Obelisk", "IO", "Schutzturm");

        /*
         * Quest Instance Maps
         */
        AddLocation(KARAMATIS, 221, 250, 206, "Karamatis 1");
        AddLocation(KARAMATIS_B, 312, 274, 206, "Karamatis 2");
        AddLocation(IDAB_PRO_L3, 221, 250, 206, "Karamatis 3");
        AddLocation(AERDINA, 275, 168, 205, "Aerdina");
        AddLocation(GERANAIA, 275, 168, 205, "Geranaia");
        // Stigma quest
        AddLocation(IDLF1B_STIGMA, 247, 249, 1392, "Sliver of Darkness", "Fragment der Finsternis");
        AddLocation(SPACE_OF_DESTINY, 246, 246, 125, "Space of Destiny", "Raum des Schicksals");
        AddLocation(ATAXIAR, 221, 250, 206, "Ataxiar 1");
        AddLocation(ATAXIAR_B, 221, 250, 206, "Ataxiar 2");
        AddLocation(BREGIRUN, 275, 168, 205, "Bregirun");
        AddLocation(NIDALBER, 275, 168, 205, "Nidalber");

        /*
         * Arenas
         */
        AddLocation(SANCTUM_UNDERGROUND_ARENA, 275, 242, 159, "Sanctum Arena", "Unterirdische Arena von Sanctum");
        AddLocation(TRINIEL_UNDERGROUND_ARENA, 275, 239, 159, "Triniel Arena", "Triniels unterirdische Arena");
        // Empyrean Crucible
        AddLocation(EMPYREAN_CRUCIBLE, 380, 350, 95, "Crucible 1-0");
        AddLocation(EMPYREAN_CRUCIBLE, 346, 350, 96, "Crucible 1-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1265, 821, 359, "Crucible 5-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1256, 797, 359, "Crucible 5-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1596, 150, 129, "Crucible 6-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1628, 155, 126, "Crucible 6-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1813, 797, 470, "Crucible 7-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1785, 797, 470, "Crucible 7-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1776, 1728, 304, "Crucible 8-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1776, 1760, 304, "Crucible 8-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1357, 1748, 320, "Crucible 9-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1334, 1741, 316, "Crucible 9-1");
        AddLocation(EMPYREAN_CRUCIBLE, 1750, 1255, 395, "Crucible 10-0");
        AddLocation(EMPYREAN_CRUCIBLE, 1761, 1280, 395, "Crucible 10-1");
        // Arena Of Chaos
        AddLocation(ARENA_OF_CHAOS, 1332, 1078, 340, "Arena of Chaos 1");
        AddLocation(ARENA_OF_CHAOS, 599, 1854, 227, "Arena of Chaos 2");
        AddLocation(ARENA_OF_CHAOS, 663, 265, 512, "Arena of Chaos 3");
        AddLocation(ARENA_OF_CHAOS, 1840, 1730, 302, "Arena of Chaos 4");
        AddLocation(ARENA_OF_CHAOS, 1932, 1228, 270, "Arena of Chaos 5");
        AddLocation(ARENA_OF_CHAOS, 1949, 946, 224, "Arena of Chaos 6");

        /*
         * Miscellaneous
         */
        // Prison
        AddLocation(LF_PRISON, 256, 256, 49, "Prison LF", "Prison Elyos");
        AddLocation(DF_PRISON, 256, 256, 49, "Prison DF", "Prison Asmos");
        // Test
        AddLocation(ID_TEST_DUNGEON, 104, 66, 25, "Test Dungeon");
        AddLocation(TEST_BASIC, 144, 136, 20, "Test Basic");
        AddLocation(TEST_SERVER, 228, 171, 49, "Test Server");
        AddLocation(TEST_GIANTMONSTER, 196, 187, 20, "Test Giantmonster");
        // Unknown
        AddLocation(NO_ZONE_NAME, 270, 200, 206, "IDAbPro");
        // GM zone
        AddLocation(HOUSING_BARRACK, 2594.29f, 85.48f, 121f, (byte)20, "GM");

        /*
         * 2.5 Maps
         */
        AddLocation(KAISINEL_ACADEMY, 459, 251, 128, "Kaisinel Academy", "Kaisinels Akademie");
        AddLocation(MARCHUTAN_PRIORY, 577, 250, 94, "Marchutan Priory", "Marchutans Konzil");
        AddLocation(ESOTERRACE, 333, 437, 326, "Esoterrace", "Esoterrasse");

        /*
         * 3.0 Maps
         */
        AddLocation(PERNON, 1069, 1539, 98, "Pernon");
        AddLocation(ORIEL, 1261, 1845, 98, "Oriel", "Elian");
        AddLocation(RENTUS_BASE, 557, 593, 154, "Rentus", "Rentus Base", "Rentus Basis");

        /*
         * 3.5
         */
        AddLocation(TIAMAT_STRONGHOLD, 1581, 1068, 492, "Tiamat Stronghold", "Tiamats Festung", "TF");
        AddLocation(DRAGON_LORDS_REFUGE, 506, 516, 242, "Dragon Lords Refuge", "Tiamats Unterschlupf", "TU");
        AddLocation(DRAGON_LORDS_REFUGE, 495, 528, 417, "Throne of Blood", "Tiamat", "Blutthron");

        /*
         * 4.0 Instances
         */
        AddLocation(INFINITY_SHARD, 109, 131, 125, "Infinity Shard", "Katalamize");

        /*
         * 4.7 Instances
         */
        AddLocation(IDGEL_DOME, 254, 179, 83, "Idgel Dome", "Ruhnatorium");
        AddLocation(LINKGATE_FOUNDRY, 362, 260, 312, "Linkgate Foundry", "Baruna Forschungslabor");
        AddLocation(INFERNAL_ILLUMINARY_OBELISK, 322.38f, 324.47f, 405.49997f, "Infernal Illuminary Obelisk", "IIO", "Schutzturm Heroisch");
        AddLocation(THE_SHUGO_EMPERORS_VAULT, 542.9366f, 299.9885f, 401f, (byte)22, "Shugo Emperors Vault");

        // New map 4.7
        AddLocation(KALDOR, 397, 1380, 163, "Kaldor");
        AddLocation(LEVINSHOR, 207, 183, 374, "Levinshor", "Akaron");
        AddLocation(BELUS, 1238, 1232, 1518, "Belus");
        AddLocation(TRANSIDIUM_ANNEX, 509, 513, 675, "Transidium Annex", "Antriksha", "Ahserion");
        AddLocation(ASPIDA, 1238, 1232, 1518, "Aspida");
        AddLocation(ATANATOS, 1238, 1232, 1518, "Atanatos", "Athanos");
        AddLocation(DISILLON, 1238, 1232, 1518, "Disillon", "Deylon");

        // New instances 4.8
        AddLocation(OCCUPIED_RENTUS_BASE, 557, 593, 154, "Occupied Rentus Base", "Occupied Rentus", "Verlorene Rentus Basis");
        AddLocation(ANGUISHED_DRAGON_LORDS_REFUGE, 506, 516, 242, "Anguished Dragon Lords Refuge", "Tiamats Hideout HM", "TU HM");
        AddLocation(DRAKENSPIRE_DEPHTS, 322, 183, 1688, "Drakenspire Depths", "Makarna");
        AddLocation(RAKSANG_RUINS, 830, 942, 1207, (byte)73, "Raksang Ruins", "Mantor");
        AddLocation(INFERNAL_DANUAR_RELIQUARY, 256.60f, 257.99f, 241.78f, "Infernal Danuar Reliquary", "IDR", "Ruhnadium Heroisch");
        AddLocation(STONESPEAR_REACH, 231.14f, 264.399f, 98, "Stonespear Reach", "Plaza of Challenge", "Platz der Herausforderung");

        // New maps 4.8
        AddLocation(CYGNEA, 2905, 803, 570, "Cygnea", "Signia");
        AddLocation(CYGNEA, 1360, 613, 583, "Kenoa");
        AddLocation(CYGNEA, 1186, 1610, 468, "Deluan");
        AddLocation(CYGNEA, 2368, 1507, 440, "Attika");
        AddLocation(CYGNEA, 2134, 2912, 329, "Außenposten der Legion des Neubeginns", "Außenposten Ldn");
        AddLocation(CYGNEA, 518, 1900, 469, "Angriffsposten der Legion des Neubeginns", "Angriffsposten Ldn");
        AddLocation(ENSHAR, 454, 2262, 220, "Enshar", "Vengar");
        AddLocation(ENSHAR, 1768, 2555, 300, "Mura");
        AddLocation(ENSHAR, 1475, 1746, 330, "Satyr");
        AddLocation(ENSHAR, 759, 1274, 253, "Velias");
        AddLocation(ENSHAR, 1578, 143, 188, "Oasentempel");
        AddLocation(ENSHAR, 2685, 1407, 341, "Tempel des Sonnenuntergangs");
        AddLocation(GRIFFOEN, 263, 127, 501, "Griffoen", "Ellegef");
        AddLocation(HABROK, 253, 107, 505, "Habrok", "Lagnatun");
        AddLocation(IDIAN_DEPTHS_LIGHT, 684, 654, 515, "Idian Depths Elyos", "Untergrund von Katalam Elyos", "UGE");
        AddLocation(IDIAN_DEPTHS_DARK, 684, 654, 515, "Idian Depths Asmo", "Untergrund von Katalam Asmo", "UGA");
    }
}
