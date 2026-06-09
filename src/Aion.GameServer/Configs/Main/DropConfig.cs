using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/DropConfig. No-default ItemQuality→ItemQuality? (nullable); Set&lt;Integer&gt;→ISet&lt;int&gt; (null, no default).</summary>
public static class DropConfig
{
    /// <summary>Property key: gameserver.drop.announce_quality</summary>
    public static ItemQuality? MIN_ANNOUNCE_QUALITY;

    /// <summary>Property key: gameserver.drop.disable_range_check_maps</summary>
    public static ISet<int> DISABLE_RANGE_CHECK_MAPS;
}
