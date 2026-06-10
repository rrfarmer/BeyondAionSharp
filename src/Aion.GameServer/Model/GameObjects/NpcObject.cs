using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Spawnengine;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/NpcObject (Rolandas) : HouseObject&lt;HousingNpc&gt;. synchronized→lock(this); canExpireNow override (HouseObject declares the Expirable default virtual). SpawnEngine/HousingNpc red-tolerated.</summary>
public class NpcObject : HouseObject<HousingNpc>
{
    private Npc npc = null;

    public NpcObject(HouseRegistry registry, int objId, int templateId) : base(registry, objId, templateId)
    {
    }

    public override void OnUse(Player player)
    {
        // TODO: Talk ?
    }

    public override void Spawn()
    {
        lock (this)
        {
            base.Spawn();
            if (npc == null)
            {
                HousingNpc template = GetObjectTemplate();
                SpawnTemplate spawn = SpawnEngine
                    .NewSingleTimeSpawn(GetOwnerHouse().GetWorldId(), template.GetNpcId(), GetX(), GetY(), GetZ(), GetHeading());
                npc = (Npc)SpawnEngine.SpawnObject(spawn, GetOwnerHouse().GetInstanceId());
            }
        }
    }

    public override void OnDespawn()
    {
        lock (this)
        {
            base.OnDespawn();
            if (npc != null)
            {
                npc.GetController().Delete();
                npc = null;
            }
        }
    }

    public override bool CanExpireNow()
    {
        lock (this)
        {
            if (npc == null)
                return true;
            return npc.GetTarget() == null;
        }
    }

    public int GetNpcObjectId()
    {
        return npc == null ? 0 : npc.GetObjectId();
    }
}
