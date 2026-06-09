using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Stats.Listeners;

/// <summary>Java parity: model/stats/listeners/TitleChangeListener. CreatureGameStats&lt;?&gt; wildcard → generic method param.</summary>
public class TitleChangeListener
{
    public static void OnBonusTitleChange<T>(CreatureGameStats<T> cgs, int titleId, bool isSet)
    {
        TitleTemplate tt = DataManager.TITLE_DATA.GetTitleTemplate(titleId);
        if (tt == null)
        {
            return;
        }
        if (!isSet)
        {
            cgs.EndEffect(tt);
        }
        else
        {
            cgs.AddEffect(tt, tt.GetModifiers());
        }
    }
}
