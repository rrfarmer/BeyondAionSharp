using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Handlers.AI;

[AIName("halloween_pumpkin")]
public class HalloweenPumpkinAI : OneDmgAI
{
    private static readonly ConcurrentDictionary<int, long> nextRewardMillisByAccountId = new ConcurrentDictionary<int, long>();

    public HalloweenPumpkinAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        if (GetNpcId() != 219484 && Rnd.Chance() < 33)
        {
            ReplaceWithLargePumpkin();
            return;
        }
        base.HandleSpawned();
    }

    public override bool CanThink()
    {
        return false;
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBase(10);
    }

    protected override void HandleDropRegistered()
    {
        if (GetNpcId() == 219484)
        {
            float chance = Rnd.Chance();
            if (chance < 1 && GetAggroList().GetMostPlayerDamage() is Player p && CanReward(p))
                ReplaceDropWithItem(162000131); // Fine Bracing Water
            else if (chance < 3)
                TryReplaceDropWithWeaponPrototype();
        }
    }

    private bool CanReward(Player player)
    {
        AtomicBoolean canReward = new AtomicBoolean();
        nextRewardMillisByAccountId.AddOrUpdate(player.AccountId, _ =>
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long nextRewardTime = nowMs + (long)TimeSpan.FromDays(1).TotalMilliseconds;
            canReward.Set(true);
            return nextRewardTime;
        }, (_, nextRewardTime) =>
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (nowMs > nextRewardTime)
            {
                nextRewardTime = nowMs + (long)TimeSpan.FromDays(1).TotalMilliseconds;
                canReward.Set(true);
            }
            return nextRewardTime;
        });
        return canReward.Get();
    }

    private void ReplaceDropWithItem(int itemId)
    {
        ISet<DropItem> drops = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(GetObjectId());
        if (drops == null)
            return;
        drops.Clear();
        drops.Add(DropRegistrationService.GetInstance().RegDropItem(1, 0, GetObjectId(), itemId, 1));
    }

    private void TryReplaceDropWithWeaponPrototype()
    {
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(GetObjectId());
        if (dropNpc == null)
            return;
        int? prototypeItemId = SelectWeaponPrototypeItemId(dropNpc.GetAllowedLooters());
        if (prototypeItemId != null)
            ReplaceDropWithItem(prototypeItemId.Value);
    }

    private int? SelectWeaponPrototypeItemId(ISet<int> allowedLooters)
    {
        List<int> itemIds = allowedLooters
            .Select(id => GetKnownList().GetObject(id) is Player player ? player : null)
            .Where(player => player != null)
            .SelectMany(player => GetWeaponPrototypeItemIds(player!))
            .ToList();
        int? result = Rnd.Get(itemIds);
        return itemIds.Count == 0 ? null : result;
    }

    private IEnumerable<int> GetWeaponPrototypeItemIds(Player player)
    {
        return player.GetPlayerClass() switch
        {
            PlayerClass.GLADIATOR => new[] { 169405391, 169405395 }, // Upgraded Distorted Sword/Polearm of the Fierce Battle Prototype
            PlayerClass.TEMPLAR => new[] { 169405391, 169405394 }, // Upgraded Distorted Sword/Greatsword of the Fierce Battle Prototype
            PlayerClass.ASSASSIN or PlayerClass.RANGER => new[] { 169405391, 169405393 }, // Upgraded Distorted Sword/Dagger of the Fierce Battle Prototype
            PlayerClass.CLERIC or PlayerClass.CHANTER => new[] { 169405392, 169405396 }, // Upgraded Distorted Mace/Staff of the Fierce Battle Prototype
            _ => Enumerable.Empty<int>(),
        };
    }

    private void ReplaceWithLargePumpkin()
    {
        GetOwner().GetController().Delete();
        SpawnTemplate spot = GetSpawnTemplate();
        if (GetSpawnTemplate().HasPool()) // despawning frees its pool spot, so we cannot reuse it
            spot = GetSpawnTemplate().GetGroup().ReserveRandomFreePoolSpot(GetOwner().GetInstanceId());
        Npc largePumpkin = new Npc(new NpcController(), spot, DataManager.NPC_DATA.GetNpcTemplate(219484));
        largePumpkin.SetKnownlist(new NpcKnownList(largePumpkin));
        largePumpkin.SetEffectController(new EffectController(largePumpkin));
        Aion.GameServer.SpawnEngine.SpawnEngine.BringIntoWorld(largePumpkin, spot.GetWorldId(), GetOwner().GetInstanceId(), spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading());
    }
}
