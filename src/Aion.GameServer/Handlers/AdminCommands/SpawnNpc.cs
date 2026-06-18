using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SpawnNpc (Luno, Neon). Spawns npcs and gatherables.</summary>
public class SpawnNpc : AdminCommand
{
    public SpawnNpc()
        : base("spawn", "Spawns npcs and gatherables.")
    {
        SetSyntaxInfo(
            "<id> - Spawns a temporary object with the specified template ID.",
            "<id> <static id> [respawn time] - Spawns an object with the specified ID and static ID (default: temporary spawn, optional: respawn time in seconds).",
            "<item link|ID> - Spawns the house object from given item link or ID.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }

        int itemId = ChatUtil.GetItemId(paramsArr[0]);
        if (itemId > 0)
        {
            SpawnHouseObject(admin, itemId);
            return;
        }

        int npcId = int.Parse(paramsArr[0]);
        int staticId = paramsArr.Length < 2 ? 0 : int.Parse(paramsArr[1]);
        int respawnTime = paramsArr.Length < 3 ? 0 : int.Parse(paramsArr[2]);

        if (DataManager.NPC_DATA.GetNpcTemplate(npcId) == null && DataManager.GATHERABLE_DATA.GetGatherableTemplate(npcId) == null)
        {
            SendInfo(admin, "Invalid npc ID.");
            return;
        }
        if (staticId < 0)
        {
            SendInfo(admin, "Invalid static ID.");
            return;
        }
        if (respawnTime < 0)
        {
            SendInfo(admin, "Invalid respawn time.");
            return;
        }

        SpawnTemplate st = Aion.GameServer.SpawnEngine.SpawnEngine.NewSpawn(admin.GetWorldId(), npcId, admin.GetX(), admin.GetY(), admin.GetZ(), admin.GetHeading(), respawnTime);
        st.SetStaticId(staticId);
        VisibleObject visibleObject = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(st, admin.GetInstanceId());
        if (respawnTime > 0 && !DataManager.SPAWNS_DATA.SaveSpawn(visibleObject, false))
            SendInfo(admin, "Could not save spawn. Npc will vanish after server restart.");
    }

    private void SpawnHouseObject(Player admin, int itemId)
    {
        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        ItemActions actions = itemTemplate == null ? null : itemTemplate.GetActions();
        SummonHouseObjectAction action = actions == null ? null : actions.GetHouseObjectAction();
        if (action == null)
        {
            SendInfo(admin, "Item is not a spawnable house item.");
            return;
        }

        HouseObject<PlaceableHouseObject> houseObject = new DummyHouseObject(action.GetTemplateId());
        houseObject.SetPosition(
            World.World.GetInstance().CreatePosition(admin.GetWorldId(), admin.GetX(), admin.GetY(), admin.GetZ(), admin.GetHeading(), admin.GetInstanceId()));
        Aion.GameServer.SpawnEngine.SpawnEngine.BringIntoWorld(houseObject);
    }

    private class DummyHouseObject : HouseObject<PlaceableHouseObject>
    {
        public DummyHouseObject(int templateId)
            : base(null, IDFactory.GetInstance().NextId(), templateId, true)
        {
        }

        public new float GetX()
        {
            return GetPosition().GetX();
        }

        public new float GetY()
        {
            return GetPosition().GetY();
        }

        public new float GetZ()
        {
            return GetPosition().GetZ();
        }

        public new sbyte GetHeading()
        {
            return (sbyte)GetPosition().GetHeading();
        }
    }
}
