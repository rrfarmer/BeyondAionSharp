using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/CosmeticItemAction.</summary>
[XmlType("CosmeticItemAction")]
public class CosmeticItemAction : AbstractItemAction
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlAttribute("name")] public string cosmeticName;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Cosmeticitems.CosmeticItemTemplate template = DataManager.COSMETIC_ITEMS_DATA.GetCosmeticItemsTemplate(cosmeticName);
        if (template == null)
        {
            return false;
        }
        if (!template.GetRace().Equals(player.GetRace()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_RACE());
            return false;
        }
        if (!template.GetGenderPermitted().Equals("ALL"))
        {
            if (!player.GetGender().ToString().Equals(template.GetGenderPermitted()))
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_GENDER());
                return false;
            }
        }
        if (player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_RESTRICTION_RIDE());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Cosmeticitems.CosmeticItemTemplate template = DataManager.COSMETIC_ITEMS_DATA.GetCosmeticItemsTemplate(cosmeticName);
        Aion.GameServer.Model.GameObjects.Players.PlayerAppearance playerAppearance = player.GetPlayerAppearance();
        string type = template.GetType_();
        int id = template.GetId();
        switch (type)
        {
            case "hair_color": playerAppearance.SetHairRGB(id); break;
            case "face_color": playerAppearance.SetSkinRGB(id); break;
            case "lip_color": playerAppearance.SetLipRGB(id); break;
            case "eye_color": playerAppearance.SetEyeRGB(id); break;
            case "hair_type": playerAppearance.SetHair(id); break;
            case "face_type": playerAppearance.SetFace(id); break;
            case "voice_type": playerAppearance.SetVoice(id); break;
            case "makeup_type": playerAppearance.SetTattoo(id); break;
            case "tattoo_type": playerAppearance.SetDeco(id); break;
            case "preset_name":
            {
                Aion.GameServer.Model.Templates.Cosmeticitems.CosmeticItemTemplate.Preset preset = template.GetPreset();
                playerAppearance.SetEyeRGB(preset.GetEyeColor());
                playerAppearance.SetLipRGB(preset.GetLipColor());
                playerAppearance.SetHairRGB(preset.GetHairColor());
                playerAppearance.SetSkinRGB(preset.GetEyeColor());
                playerAppearance.SetHair(preset.GetHairType());
                playerAppearance.SetFace(preset.GetFaceType());
                playerAppearance.SetHeight(preset.GetScale());
                player.GetAccountData().UpdateBoundingRadius();
                break;
            }
            default:
            {
                log.LogWarning("Unhandled cosmetic item type: " + type);
                return;
            }
        }
        Aion.GameServer.Dao.PlayerAppearanceDAO.Store(player);
        player.GetInventory().Delete(targetItem);
        player.GetController().OnChangedPlayerAttributes();
    }
}
