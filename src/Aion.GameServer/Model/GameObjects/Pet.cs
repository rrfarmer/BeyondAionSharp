using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Pet extends VisibleObject.</summary>
public class Pet : VisibleObject
{
    private readonly Player.Player master;
    private CreatureMoveController<Pet> moveController;
    private readonly Aion.GameServer.Model.GameObjects.Players.PetCommonData commonData;

    public Pet(Aion.GameServer.Model.Templates.Pet.PetTemplate petTemplate, Aion.GameServer.Controllers.PetController controller, Aion.GameServer.Model.GameObjects.Players.PetCommonData commonData, Player.Player master)
        : base(commonData.GetObjectId(), controller, null, petTemplate, new WorldPosition(master.GetWorldId()), false)
    {
        controller.SetOwner(this);
        this.master = master;
        this.commonData = commonData;
        // Java parity: new CreatureMoveController<Pet>(this){} — anon empty subclass; C# needs a concrete one.
        this.moveController = new PetDefaultMoveController(this);
    }

    public override string Name => commonData.GetName();

    public Player.Player GetMaster()
    {
        return master;
    }

    public Aion.GameServer.Model.GameObjects.Players.PetCommonData GetCommonData()
    {
        return commonData;
    }

    public CreatureMoveController<Pet> GetMoveController()
    {
        return moveController;
    }

    public override Aion.GameServer.Model.Templates.Pet.PetTemplate GetObjectTemplate()
    {
        return (Aion.GameServer.Model.Templates.Pet.PetTemplate)base.GetObjectTemplate();
    }

    // Java parity: anonymous CreatureMoveController<Pet> subclass (empty).
    private sealed class PetDefaultMoveController : CreatureMoveController<Pet>
    {
        public PetDefaultMoveController(Pet owner) : base(owner)
        {
        }
    }
}
