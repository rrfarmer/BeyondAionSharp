using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Stats;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>
/// Container holding/serving all NpcTemplate instances (Java parity: dataholders/NpcData, Luno). @XmlRootElement(npc_templates)→
/// [XmlRoot]; @XmlTransient Map/Set→[XmlIgnore] Dictionary/HashSet (final→readonly); afterUnmarshal→StaticDataListener async-or-run
/// (u→parent; this::init→Init method group); signed byte level→sbyte; addAll→UnionWith; npcs nulled. StaticDataListener/
/// NpcStatCalculation/StatsTemplate/DialogAction.NameOf red-tolerated.
/// </summary>
[XmlRoot("npc_templates")]
public class NpcData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("npc_template")]
    public List<NpcTemplate> npcs;

    [XmlIgnore]
    private readonly Dictionary<int, NpcTemplate> npcData = new();

    [XmlIgnore]
    private readonly HashSet<int> functionDialogIds = new();

    public void AfterUnmarshal(object parent)
    {
        StaticDataListener.RegisterForAsyncExecutionOrRun(parent, Init);
    }

    private void Init()
    {
        foreach (NpcTemplate npc in npcs)
        {
            npcData[npc.GetTemplateId()] = npc;
            if (npc.GetFuncDialogIds() != null)
            {
                foreach (int dialogActionId in npc.GetFuncDialogIds())
                {
                    if (DialogAction.NameOf(dialogActionId) == null)
                        log.LogWarning("Unknown dialog action " + dialogActionId + " for Npc " + npc.GetTemplateId());
                }
            }
            if (npc.GetTribe() != TribeClass.PET && npc.GetTribe() != TribeClass.PET_DARK) // summons and siege weapons have fixed stats
            {
                NpcRating rating = npc.GetRating();
                NpcRank rank = npc.GetRank();
                sbyte level = npc.GetLevel();
                StatsTemplate template = npc.GetStatsTemplate();
                if (template.GetAttack() == 0)
                    template.SetAttack(NpcStatCalculation.CalculateStat(StatEnum.PHYSICAL_ATTACK, rating, rank, level));
                if (template.GetAccuracy() == 0)
                    template.SetAccuracy(NpcStatCalculation.CalculateStat(StatEnum.PHYSICAL_ACCURACY, rating, rank, level));
                if (template.GetMagicalAttack() == 0)
                    template.SetMagicalAttack(NpcStatCalculation.CalculateStat(StatEnum.MAGICAL_ATTACK, rating, rank, level));
                if (template.GetMacc() == 0)
                    template.SetMacc(NpcStatCalculation.CalculateStat(StatEnum.MAGICAL_ACCURACY, rating, rank, level));
                if (template.GetMresist() == 0)
                    template.SetMresist(NpcStatCalculation.CalculateStat(StatEnum.MAGICAL_RESIST, rating, rank, level));
                if (template.GetMdef() == 0)
                    template.SetMdef(NpcStatCalculation.CalculateStat(StatEnum.MAGICAL_DEFEND, rating, rank, level));
                if (template.GetMcrit() == 0)
                    template.SetMcrit(50);
                if (template.GetPcrit() == 0)
                    template.SetPcrit(10);
                if (template.GetPdef() == 0)
                    template.SetPdef(NpcStatCalculation.CalculateStat(StatEnum.PHYSICAL_DEFENSE, rating, rank, level));
                if (template.GetParry() == 0)
                    template.SetParry(NpcStatCalculation.CalculateStat(StatEnum.PARRY, rating, rank, level));
                if (level >= 50 && template.GetSpellResist() == 0)
                    template.SetSpellResist(NpcStatCalculation.CalculateStat(StatEnum.MAGICAL_CRITICAL_RESIST, rating, rank, level));
                if (level >= 50 && template.GetStrikeResist() == 0)
                {
                    int strikeResist = NpcStatCalculation.CalculateStat(StatEnum.PHYSICAL_CRITICAL_RESIST, rating, rank, level);
                    if (strikeResist > 700) // In general strike resist cannot exceed 700 in retail templates, except bosses in Drakenspire Depths
                        strikeResist = 700;
                    template.SetStrikeResist(strikeResist);
                }
                template.SetStunLikeResistance(NpcStatCalculation.CalculateStat(StatEnum.STUNLIKE_RESISTANCE, rating, rank, level));
            }
            if (npc.GetFuncDialogIds() != null)
                functionDialogIds.UnionWith(npc.GetFuncDialogIds());
        }
        npcs = null;
    }

    public int Size()
    {
        return npcData.Count;
    }

    public NpcTemplate GetNpcTemplate(int id)
    {
        return npcData.GetValueOrDefault(id);
    }

    public ICollection<NpcTemplate> GetNpcData()
    {
        return npcData.Values;
    }

    public bool IsFunctionDialog(int functionDialogId)
    {
        return functionDialogIds.Contains(functionDialogId);
    }
}
