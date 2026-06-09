using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/HouseDecoration (Rolandas). extends AionObject implements Persistable. Java getName() override → C# overrides abstract Name property (GetName()=>Name bridges).</summary>
public class HouseDecoration : AionObject, IPersistable
{
    private readonly int templateId;
    private sbyte room;
    private PersistentState persistentState;

    public HouseDecoration(int objectId, int templateId) : this(objectId, templateId, -1)
    {
    }

    public HouseDecoration(int objectId, int templateId, int room) : base(objectId)
    {
        this.templateId = templateId;
        this.room = (sbyte)room;
        this.persistentState = PersistentState.NEW;
    }

    public int GetTemplateId()
    {
        return templateId;
    }

    public HousePart GetTemplate()
    {
        return DataManager.HOUSE_PARTS_DATA.GetPartById(templateId);
    }

    public PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(PersistentState persistentState)
    {
        this.persistentState = persistentState;
    }

    public override string Name => GetTemplate().GetName();

    public sbyte GetRoom()
    {
        return room;
    }

    public void SetRoom(int value)
    {
        room = (sbyte)value;
    }
}
