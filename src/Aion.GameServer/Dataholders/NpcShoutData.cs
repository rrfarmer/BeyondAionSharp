using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Npcshout;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/NpcShoutData (Rolandas). @XmlRootElement(npc_shouts); afterUnmarshal→AfterUnmarshal(object); LinkedHashMap→Dictionary; removeIf→RemoveAll.</summary>
[XmlRoot("npc_shouts")]
public class NpcShoutData
{
    [XmlElement("shout_group")] public List<ShoutGroup> shoutGroups;

    [XmlIgnore] private readonly Dictionary<int, Dictionary<int, List<NpcShout>>> shoutsByWorldNpcs = new();

    [XmlIgnore] private int count = 0;

    public void AfterUnmarshal(object parent)
    {
        foreach (ShoutGroup group in shoutGroups)
        {
            for (int i = group.GetShoutNpcs().Count - 1; i >= 0; i--)
            {
                ShoutList shoutList = group.GetShoutNpcs()[i];
                int worldId = shoutList.GetRestrictWorld();

                // Java parity: computeIfAbsent for the world and the npc, and the parsed lists are left untouched.
                if (!shoutsByWorldNpcs.TryGetValue(worldId, out Dictionary<int, List<NpcShout>>? worldShouts) || worldShouts == null)
                {
                    worldShouts = new Dictionary<int, List<NpcShout>>();
                    this.shoutsByWorldNpcs[worldId] = worldShouts;
                }
                this.count += shoutList.GetNpcShouts().Count;
                for (int j = shoutList.GetNpcIds().Count - 1; j >= 0; j--)
                {
                    int npcId = shoutList.GetNpcIds()[j];
                    if (!worldShouts.TryGetValue(npcId, out List<NpcShout>? shouts) || shouts == null)
                    {
                        shouts = new List<NpcShout>();
                        worldShouts[npcId] = shouts;
                    }
                    shouts.AddRange(shoutList.GetNpcShouts());
                }
            }
        }
        this.shoutGroups = null;
    }

    public int Size()
    {
        return this.count;
    }

    /// <summary>Get global npc shouts plus world specific shouts. Returns null if not found.</summary>
    public List<NpcShout> GetNpcShouts(int worldId, int npcId)
    {
        Dictionary<int, List<NpcShout>> globalShouts = shoutsByWorldNpcs.TryGetValue(0, out var gs) ? gs : null;
        Dictionary<int, List<NpcShout>> worldShouts = shoutsByWorldNpcs.TryGetValue(worldId, out var ws) ? ws : null;
        List<NpcShout> globalNpcShouts = globalShouts == null ? null : (globalShouts.TryGetValue(npcId, out var g) ? g : null);
        List<NpcShout> worldNpcShouts = worldShouts == null ? null : (worldShouts.TryGetValue(npcId, out var w) ? w : null);

        if (globalShouts == null && worldShouts == null)
            return null;

        List<NpcShout> npcShouts = new List<NpcShout>();
        if (globalNpcShouts != null)
            npcShouts.AddRange(globalNpcShouts);
        if (worldNpcShouts != null)
            npcShouts.AddRange(worldNpcShouts);
        return npcShouts;
    }

    public List<NpcShout> GetNpcShouts(int worldId, int npcId, ShoutEventType? type)
    {
        List<NpcShout> shouts = GetNpcShouts(worldId, npcId);
        if (shouts == null || type == null)
            return shouts;

        return shouts.Where(shout => shout.GetWhen() == type).ToList();
    }

    /// <summary>Gets shouts for npc filtered by pattern (null = all) and skill number (0 = all).</summary>
    public List<NpcShout> GetNpcShouts(int worldId, int npcId, ShoutEventType? type, string pattern, int skillNo)
    {
        List<NpcShout> shouts = GetNpcShouts(worldId, npcId, type);
        if (shouts == null)
            return shouts;

        shouts.RemoveAll(shout => pattern != null && !pattern.Equals(shout.GetPattern()) || skillNo != 0 && skillNo != shout.GetSkillNo());
        return shouts;
    }
}
