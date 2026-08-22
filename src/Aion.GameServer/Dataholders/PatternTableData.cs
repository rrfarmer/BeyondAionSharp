using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Ai.Pattern;

namespace Aion.GameServer.Dataholders;

/// <summary>
/// A retail pattern table: the branches an npc runs, stored as data rather than compiled C#.
/// </summary>
/// <remarks>
/// One type serves every table -- death spawns, wake and idle patterns, battle cycles -- because they
/// differ only in which handler a branch belongs to, and that is an attribute. Each file is loaded
/// into its own instance.
/// <para>
/// <b>Branch order is document order, deliberately.</b> The tables are not sorted by priority: the
/// death table emits priority 7 before priority 8 for the same npc, and branch lists are
/// first-match-wins, so re-sorting on load would quietly change which rung fires.
/// </para>
/// <para>
/// <b>A file that cannot be read is refused whole.</b> <see cref="PatternTableLoader"/> throws on a
/// token it cannot translate and nothing here catches it. Skipping the branch would leave a boss with
/// most of its rotation, still fighting, still looking alive, and wrong in a way no test would see.
/// </para>
/// </remarks>
[XmlRoot("pattern_table")]
public class PatternTableData
{
    [XmlElement("npc")] public List<PatternTableNpc>? npcs;

    [XmlIgnore] private readonly Dictionary<string, Dictionary<int, PatternBranch[]>> byHandler = new();

    /// <summary>How many npcs this table speaks for, across every handler.</summary>
    [XmlIgnore] public int Size { get; private set; }

    /// <summary>The branches one npc runs for one handler, or empty.</summary>
    public PatternBranch[] For(int npcId, string handler = "")
        => byHandler.TryGetValue(handler, out Dictionary<int, PatternBranch[]>? table)
            && table.TryGetValue(npcId, out PatternBranch[]? branches)
            ? branches
            : [];

    /// <summary>Every npc this table drives, across every handler.</summary>
    public IEnumerable<int> AllNpcs
    {
        get
        {
            HashSet<int> seen = new();
            foreach (Dictionary<int, PatternBranch[]> table in byHandler.Values)
            {
                foreach (int npc in table.Keys)
                {
                    seen.Add(npc);
                }
            }

            return seen;
        }
    }

    /// <summary>Every npc this table drives, for one handler.</summary>
    public IEnumerable<int> Npcs(string handler = "")
        => byHandler.TryGetValue(handler, out Dictionary<int, PatternBranch[]>? table)
            ? table.Keys
            : [];

    public void AfterUnmarshal(object parent)
    {
        byHandler.Clear();
        HashSet<int> distinct = new();
        if (npcs == null)
        {
            Size = 0;
            return;
        }

        foreach (PatternTableNpc npc in npcs)
        {
            if (npc.branches == null || npc.branches.Count == 0)
            {
                continue;
            }

            PatternBranch[] built = new PatternBranch[npc.branches.Count];
            for (int i = 0; i < npc.branches.Count; i++)
            {
                built[i] = Build(npc, npc.branches[i]);
            }

            if (!byHandler.TryGetValue(npc.handler ?? string.Empty,
                    out Dictionary<int, PatternBranch[]>? table))
            {
                table = new Dictionary<int, PatternBranch[]>();
                byHandler[npc.handler ?? string.Empty] = table;
            }

            table[npc.npcId] = built;
            distinct.Add(npc.npcId);
        }

        Size = distinct.Count;
        npcs = null;
    }

    private static PatternBranch Build(PatternTableNpc npc, PatternTableBranch branch)
    {
        // No guards means the branch always matches: Evaluate fails a branch only when a condition
        // says no, so an empty list is retail's unguarded rung.
        PatternCondition[] guards = branch.guards == null || branch.guards.Count == 0
            ? []
            : new PatternCondition[branch.guards.Count];
        for (int i = 0; i < (branch.guards?.Count ?? 0); i++)
        {
            guards[i] = PatternTableLoader.Guard(branch.guards![i].token ?? string.Empty);
        }

        PatternAction[] actions = new PatternAction[branch.actions?.Count ?? 0];
        for (int i = 0; i < actions.Length; i++)
        {
            PatternTableAction row = branch.actions![i];
            actions[i] = PatternTableLoader.Action(new PatternTableLoader.ActionRow(
                row.kind ?? string.Empty, row.a1, row.a2, row.a3, row.place ?? string.Empty,
                row.x, row.y, row.z, row.group));
        }

        return AiPattern.Branch(branch.priority, branch.comment ?? string.Empty, guards, actions);
    }
}

/// <summary>One npc's branches for one handler.</summary>
public class PatternTableNpc
{
    [XmlAttribute("id")] public int npcId;

    /// <summary>Retail's handler name. Absent on tables that have only one.</summary>
    [XmlAttribute("handler")] public string? handler;

    [XmlElement("branch")] public List<PatternTableBranch>? branches;
}

/// <summary>One rung: what has to be true, and what it does.</summary>
public class PatternTableBranch
{
    [XmlAttribute("priority")] public int priority;

    [XmlAttribute("comment")] public string? comment;

    [XmlElement("guard")] public List<PatternTableGuard>? guards;

    [XmlElement("action")] public List<PatternTableAction>? actions;
}

/// <summary>One guard, as the extractor's token.</summary>
public class PatternTableGuard
{
    [XmlAttribute("token")] public string? token;
}

/// <summary>One action: its kind, and the numbers that fill it in.</summary>
public class PatternTableAction
{
    [XmlAttribute("kind")] public string? kind;

    [XmlAttribute("a1")] public string a1 = "0";

    [XmlAttribute("a2")] public string a2 = "0";

    [XmlAttribute("a3")] public string a3 = "0";

    [XmlAttribute("place")] public string? place;

    [XmlAttribute("x")] public string x = "0";

    [XmlAttribute("y")] public string y = "0";

    [XmlAttribute("z")] public string z = "0";

    /// <summary>Retail's spawn id. Absent where a table does not track what it placed.</summary>
    [XmlAttribute("group")] public string group = "";
}
