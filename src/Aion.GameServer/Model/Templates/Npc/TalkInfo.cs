using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npc;

/// <summary>
/// NPC talk/dialog parameters.
/// Java parity: model/templates/npc/TalkInfo (@XmlType("TalkInfo")).
/// </summary>
[XmlType("TalkInfo")]
public class TalkInfo
{
    [XmlAttribute("distance")] public int TalkDistance { get; set; } = 2;
    [XmlAttribute("delay")] public int TalkDelay { get; set; }
    [XmlAttribute("is_dialog")] public bool HasDialog { get; set; }
    [XmlAttribute("func_dialogs")] public string? FuncDialogIdsRaw { get; set; }
    // Java parity: @XmlAttribute SubDialogType subDialogType is null when the attribute is absent. A C# non-nullable
    // enum would default to ordinal 0 (FORT_CAPTURE) and wrongly restrict every plain NPC's dialog. Back it with a
    // nullable string proxy so an absent attribute -> null (XmlSerializer can't represent a nullable-enum attribute).
    [XmlAttribute("subdialog_type")] public string? SubDialogTypeRaw { get; set; }
    [XmlAttribute("subdialog_value")] public int SubDialogValue { get; set; }
    [XmlAttribute("can_talk_invisible")] public bool CanTalkInvisible { get; set; } = true;

    private List<int>? _funcDialogIds;
    private SubDialogType? _subDialogType;
    private bool _subDialogTypeParsed;

    public int GetDistance() => TalkDistance;
    public int GetDelay() => TalkDelay;
    public bool IsDialogNpc() => HasDialog;

    // Java parity: getFuncDialogIds() — @XmlAttribute List<Integer> (space-separated).
    public List<int>? GetFuncDialogIds()
    {
        if (_funcDialogIds == null && FuncDialogIdsRaw != null)
            _funcDialogIds = FuncDialogIdsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        return _funcDialogIds;
    }

    public SubDialogType? GetSubDialogType()
    {
        if (!_subDialogTypeParsed)
        {
            _subDialogType = string.IsNullOrEmpty(SubDialogTypeRaw) ? (SubDialogType?)null : System.Enum.Parse<SubDialogType>(SubDialogTypeRaw);
            _subDialogTypeParsed = true;
        }
        return _subDialogType;
    }

    public int GetSubDialogValue() => SubDialogValue;
    public bool IsCanTalkInvisible() => CanTalkInvisible;
}
