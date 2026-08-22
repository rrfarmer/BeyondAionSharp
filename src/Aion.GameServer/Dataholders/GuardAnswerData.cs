using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders;

/// <summary>
/// <c>ai/guard_answers.xml</c>: which npcs answer a guard's call for help, and with what weight.
/// </summary>
/// <remarks>
/// <b>This has no Java counterpart, and that is deliberate.</b> The table is retail's, extracted from
/// the 5.8 pattern dump; aionemu has nothing equivalent. It used to be a 3,700-line dictionary literal
/// compiled into <c>GuardAnswers</c>, which is data wearing code's clothes — it made that file the
/// third largest in the port and there was no way to change a single hate weight without a rebuild.
/// <para>
/// Loaded the same way as every other single-file holder, so the usual rules apply: the file is
/// generated, the extractor owns it, and hand edits are lost on the next run.
/// </para>
/// </remarks>
[XmlRoot("guard_answers")]
public class GuardAnswerData
{
    [XmlElement("guard")] public List<GuardAnswerSet>? guards;

    [XmlIgnore] private readonly Dictionary<int, GuardAnswerRow[]> byNpc = new();

    /// <summary>Every npc that answers a call, and the calls it answers.</summary>
    public IReadOnlyDictionary<int, GuardAnswerRow[]> Rows => byNpc;

    /// <summary>How many npcs the table speaks for.</summary>
    public int Size => byNpc.Count;

    public void AfterUnmarshal(object parent)
    {
        byNpc.Clear();
        if (guards == null)
        {
            return;
        }

        foreach (GuardAnswerSet set in guards)
        {
            if (set.answers == null || set.answers.Count == 0)
            {
                continue;
            }

            byNpc[set.npcId] = set.answers.ToArray();
        }

        // The parsed form is only needed to build the lookup; releasing it keeps the holder the size
        // of the data rather than twice it, which matters at 3,696 entries.
        guards = null;
    }
}

/// <summary>One npc's answers.</summary>
public class GuardAnswerSet
{
    [XmlAttribute("npc_id")] public int npcId;

    [XmlElement("answer")] public List<GuardAnswerRow>? answers;
}

/// <summary>One call an npc answers.</summary>
public class GuardAnswerRow
{
    /// <summary>The message number the caller broadcasts.</summary>
    [XmlAttribute("call")] public int call;

    /// <summary>Retail's hate weight for a guard that is standing about.</summary>
    [XmlAttribute("idle")] public int idle;

    /// <summary>Retail's hate weight for a guard already in a fight. Negative means it does not answer.</summary>
    [XmlAttribute("busy")] public int busy;

    /// <summary>
    /// True for the calls that name the caller rather than a player. Those are not turned into pattern
    /// rungs; the classes that answer them carry their own actions.
    /// </summary>
    [XmlAttribute("aims_at_sender")] public bool aimsAtSender;

    // The fields above carry the XML attribute names; these are how the rest of the port reads them.
    public int Call => call;

    public int Idle => idle;

    public int Busy => busy;

    public bool AimsAtSender => aimsAtSender;
}
