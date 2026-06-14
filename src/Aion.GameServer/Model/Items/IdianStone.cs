using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.Items;

/// <summary>Java parity: model/items/IdianStone extends ItemStone.</summary>
public class IdianStone : ItemStone
{
    private ActionObserver actionListener;
    private int polishCharge;
    private readonly Item item;
    private readonly int burnDefend;
    private readonly int burnAttack;
    private readonly RandomBonusEffect rndBonusEffect;

    public IdianStone(int itemId, Aion.GameServer.Model.GameObjects.IPersistable.PersistentState persistentState, Item item, int polishNumber, int polishCharge)
        : base(item.GetObjectId(), itemId, 0, persistentState)
    {
        this.item = item;
        burnDefend = item.GetItemTemplate().GetIdianAction().GetBurnDefend();
        burnAttack = item.GetItemTemplate().GetIdianAction().GetBurnAttack();
        this.polishCharge = polishCharge;
        Aion.GameServer.Model.Templates.Items.Actions.ItemActions actions = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetActions();
        rndBonusEffect = new RandomBonusEffect(Aion.GameServer.Model.Templates.Items.Bonuses.StatBonusType.POLISH, actions.GetPolishAction().GetPolishSetId(), polishNumber);
    }

    public void OnEquip(Aion.GameServer.Model.GameObjects.Players.Player player, long slot)
    {
        if (polishCharge > 0 && (slot & ItemSlot.MAIN_HAND.GetSlotIdMask()) != 0)
        {
            actionListener = new IdianActionObserver(this, player);
            player.GetObserveController().AddObserver(actionListener);
            rndBonusEffect.ApplyEffect(player);
        }
    }

    // Java parity: anonymous ActionObserver(DOT_ATTACK_DEFEND) in onEquip.
    private sealed class IdianActionObserver : ActionObserver
    {
        private readonly IdianStone outer;
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;

        public IdianActionObserver(IdianStone outer, Aion.GameServer.Model.GameObjects.Players.Player player) : base(ObserverType.DOT_ATTACK_DEFEND)
        {
            this.outer = outer;
            this.player = player;
        }

        public override void Dotattacked(Creature creature, Effect dotEffect)
        {
            outer.DecreasePolishCharge(player, true);
        }

        public override void Attacked(Creature creature, int skillId)
        {
            outer.DecreasePolishCharge(player, true);
        }

        public override void Attack(Creature creature, int skillId)
        {
            if (skillId == 0)
                outer.DecreasePolishCharge(player, false);
        }
    }

    private void DecreasePolishCharge(Aion.GameServer.Model.GameObjects.Players.Player player, bool isAttacked)
    {
        DecreasePolishCharge(player, isAttacked, 0);
    }

    public void DecreasePolishCharge(Aion.GameServer.Model.GameObjects.Players.Player player, int skillValue)
    {
        DecreasePolishCharge(player, false, skillValue);
    }

    private void DecreasePolishCharge(Aion.GameServer.Model.GameObjects.Players.Player player, bool isAttacked, int skillValue)
    {
        lock (this)
        {
            int result;
            if (polishCharge <= 0)
            {
                return;
            }
            if (skillValue == 0)
                result = isAttacked ? burnDefend : burnAttack;
            else
                result = skillValue;
            if (polishCharge - result < 0)
            {
                polishCharge = 0;
            }
            else
            {
                polishCharge -= result;
            }
            if (polishCharge <= 300000 && polishCharge + result > 300000) // we just dropped to or below 300k
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_INVENTORY_UPDATE_ITEM(player, item, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.POLISH_CHARGE));
            }
            else if (polishCharge < 0)
            {
                polishCharge = 0;
            }
            if (polishCharge == 0)
            {
                OnUnEquip(player);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_INVENTORY_UPDATE_ITEM(player, item));
                item.SetIdianStone(null);
                SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.DELETED);
                Aion.GameServer.Dao.ItemStoneListDAO.StoreIdianStones(this);
            }
        }
    }

    public int GetPolishNumber()
    {
        return rndBonusEffect.GetStatBonusId();
    }

    public int GetPolishCharge()
    {
        return polishCharge;
    }

    public void OnUnEquip(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (actionListener != null)
        {
            rndBonusEffect.EndEffect(player);
            player.GetObserveController().RemoveObserver(actionListener);
            actionListener = null;
        }
    }
}
