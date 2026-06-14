using System;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.Items;

/// <summary>Java parity: model/items/ChargeInfo extends ActionObserver.</summary>
public class ChargeInfo : ActionObserver
{
    public const int LEVEL2 = 1000000;
    public const int LEVEL1 = 500000;
    private readonly int attackBurn;
    private readonly int defendBurn;
    private readonly Item item;
    private int chargePoints;
    private int playerId;

    public ChargeInfo(int chargePoints, Item item) : base(ObserverType.DOT_ATTACK_DEFEND)
    {
        this.chargePoints = chargePoints;
        this.item = item;
        if (item.GetImprovement() != null)
        {
            this.attackBurn = item.GetImprovement().GetBurnAttack();
            this.defendBurn = item.GetImprovement().GetBurnDefend();
        }
        else
        {
            this.attackBurn = 0;
            this.defendBurn = 0;
        }
    }

    public int GetChargePoints()
    {
        return chargePoints;
    }

    private Aion.GameServer.Model.GameObjects.Players.Player GetPlayer()
    {
        return playerId == 0 ? null : Aion.GameServer.World.World.GetInstance().GetPlayer(playerId);
    }

    public void SetPlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        this.playerId = player == null ? 0 : player.GetObjectId();
    }

    /// <summary>
    /// Updates the chargePoints of the item.
    /// </summary>
    /// <param name="pointsToAdd">chargePoints to add to the current charge points</param>
    /// <returns>whether the visual charge bar has changed or not</returns>
    public bool UpdateChargePoints(int pointsToAdd)
    {
        lock (this)
        {
            int newChargePoints = chargePoints + pointsToAdd;
            newChargePoints = Math.Max(0, Math.Min(newChargePoints, LEVEL2));
            int currentChargeBarStep = chargePoints / 50000;
            int newChargeBarStep = newChargePoints / 50000;
            chargePoints = newChargePoints;
            Aion.GameServer.Model.GameObjects.Players.Player player;
            if (item.IsEquipped() && (player = GetPlayer()) != null)
                player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            item.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            return currentChargeBarStep != newChargeBarStep;
        }
    }

    public override void Dotattacked(Creature creature, Effect dotEffect)
    {
        if (UpdateChargePoints(-defendBurn))
            SendItemUpdate();
    }

    public override void Attacked(Creature creature, int skillId)
    {
        if (skillId == 0 && UpdateChargePoints(-defendBurn))
            SendItemUpdate();
    }

    public override void Attack(Creature creature, int skillId)
    {
        if (skillId == 0 && UpdateChargePoints(-attackBurn))
            SendItemUpdate();
    }

    private void SendItemUpdate()
    {
        Aion.GameServer.Model.GameObjects.Players.Player player = GetPlayer();
        if (player != null)
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_INVENTORY_UPDATE_ITEM(player, item, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.CHARGE));
    }
}
