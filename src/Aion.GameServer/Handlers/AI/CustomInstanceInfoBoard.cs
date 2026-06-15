using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/custom/eternalChallenge/CustomInstanceInfoBoard (Neon).</summary>
[AIName("custom_instance_info_board")]
public class CustomInstanceInfoBoard : GeneralNpcAI
{
    private static readonly Dictionary<int, long> lastLeaderboardOpenTime = new Dictionary<int, long>();

    public CustomInstanceInfoBoard(Npc owner)
        : base(owner)
    {
        owner.SetMasterName("Eternal Challenge");
    }

    protected override void HandleDialogStart(Player player)
    {
        lock (lastLeaderboardOpenTime)
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (lastLeaderboardOpenTime.TryGetValue(player.GetObjectId(), out long lastOpenTime) && lastOpenTime + 3000 > now)
                return; // simple flood protection so we don't have to cache the whole leaderboard
            lastLeaderboardOpenTime[player.GetObjectId()] = now;
        }
        CustomInstanceService.GetInstance().OpenLeaderboard(player, GetOwner().GetRace());
    }
}
