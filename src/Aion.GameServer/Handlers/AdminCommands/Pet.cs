using System.Drawing;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Services.ToyPet;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Pet (ATracer, Neon).</summary>
public class Pet : AdminCommand
{
    public Pet()
        : base("pet", "Adds or removes a pet.")
    {
        SetSyntaxInfo(
            "<list> - Lists all available Pet IDs.",
            "<add> <pet id> <name> - Adds the pet with the specified ID and names it.",
            "<del> <pet id> - Deletes the pet with the specified ID.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        string action = paramsArr[0];
        if (action.Equals("list", System.StringComparison.OrdinalIgnoreCase))
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("List of pets:");
            foreach (int id in DataManager.PET_DATA.GetPetIds().OrderBy(x => x))
            {
                PetTemplate template = DataManager.PET_DATA.GetPetTemplate(id);
                sb.Append('\n');
                sb.Append(template.GetTemplateId());
                sb.Append(" - ");
                sb.Append(ChatUtil.Color(ChatUtil.Capitalize(template.GetName()), Color.White));
                sb.Append("\n\tFunctions: ");
                var iter = template.GetPetFunctions().GetEnumerator();
                bool hasNext = iter.MoveNext();
                while (hasNext)
                {
                    PetFunction current = iter.Current;
                    hasNext = iter.MoveNext();
                    sb.Append(current.GetPetFunctionType() + (hasNext ? ", " : ""));
                }
            }
            SendInfo(admin, sb.ToString());
        }
        else
        {
            int petId;

            if (paramsArr.Length < 2)
            {
                SendInfo(admin, "You must specify the pet ID.");
                return;
            }
            if (!int.TryParse(paramsArr[1], out petId) || DataManager.PET_DATA.GetPetTemplate(petId) == null)
            {
                SendInfo(admin, "Pet ID is invalid.");
                return;
            }

            if (action.Equals("add", System.StringComparison.OrdinalIgnoreCase))
            {
                if (paramsArr.Length != 3)
                {
                    SendInfo(admin, "You must specify a name for the pet.");
                    return;
                }
                PetAdoptionService.AddPet(admin, petId, paramsArr[2], 0, 0);
            }
            else if (action.Equals("del", System.StringComparison.OrdinalIgnoreCase))
            {
                PetAdoptionService.SurrenderPet(admin, petId);
            }
        }
    }
}
