using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Chest;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ChestAI (ATracer, xTz).</summary>
[AIName("chest")]
public class ChestAI : ActionItemNpcAI
{
    private static readonly ILogger log = NullLogger.Instance;
    private ChestTemplate chestTemplate;

    public ChestAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        chestTemplate = DataManager.CHEST_DATA.GetChestTemplate(GetNpcId());

        if (chestTemplate == null)
        {
            log.LogWarning("Missing chest template or incorrect AI for npc " + GetNpcId());
            return;
        }
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (TryOpening(player))
        {
            if (GetOwner().IsInState(CreatureState.DEAD))
            {
                AuditLogger.Log(player, "attempted multiple chest looting!");
                return;
            }

            ICollection<Player> players = new HashSet<Player>();
            TemporaryPlayerTeam playerTeam = player.GetCurrentTeam();
            if (playerTeam != null)
            {
                int range = DropConfig.DISABLE_RANGE_CHECK_MAPS.Contains(GetPosition().GetMapId()) ? 9999 : GroupConfig.GROUP_MAX_DISTANCE;
                foreach (Player member in playerTeam.GetOnlineMembers())
                {
                    if (PositionUtil.IsInRange(member, GetOwner(), range))
                        players.Add(member);
                }
            }
            if (players.Count == 0) // no team or nobody was in range
                players.Add(player);
            DropRegistrationService.GetInstance().RegisterDrop(GetOwner(), player, GetHighestLevel(players), players);
            AIActions.Die(this, player);
            DropService.GetInstance().RequestDropList(player, GetObjectId());
            base.HandleUseItemFinish(player);
        }
        else
        {
            PacketSendUtility.SendMonologue(player, 1111301); // I'll need a key to open this.
        }
    }

    private bool TryOpening(Player player)
    {
        List<KeyItem> keyItems = chestTemplate.GetKeyItems();
        foreach (KeyItem keyItem in keyItems)
        { // check if enough key items are available
            if (keyItem.GetItemIds() != null && keyItem.GetItemIds().Any(itemId => itemId == 0)) // chest can be opened w/o keys
                return true;
            long availableKeys = 0;
            foreach (int keyItemId in keyItem.GetItemIds())
            {
                availableKeys += player.GetInventory().GetItemCountByItemId(keyItemId);
            }
            if (availableKeys < keyItem.GetCount())
                return false;
        }
        foreach (KeyItem keyItem in keyItems)
        { // remove key items
            long keyCountToDecrease = keyItem.GetCount();
            foreach (int keyItemId in keyItem.GetItemIds())
            {
                long availableKeys = player.GetInventory().GetItemCountByItemId(keyItemId);
                if (availableKeys >= keyCountToDecrease)
                {
                    player.GetInventory().DecreaseByItemId(keyItemId, keyCountToDecrease);
                    keyCountToDecrease = 0;
                }
                else
                {
                    keyCountToDecrease -= availableKeys;
                    player.GetInventory().DecreaseByItemId(keyItemId, availableKeys);
                }
            }
        }
        return true;
    }

    private int GetHighestLevel(ICollection<Player> players)
    {
        return players.Select(p => p.GetLevel()).Max();
    }
}
