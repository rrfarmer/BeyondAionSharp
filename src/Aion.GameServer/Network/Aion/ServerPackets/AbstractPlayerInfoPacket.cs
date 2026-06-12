using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/AbstractPlayerInfoPacket (AEJTester, Nemesiss, Niato, Neon). Base for character-list/select packets: writePlayerInfo (full account-data appearance + legion + visible items + ban info) and writeEquippedItems (in-world equipment mask). System.currentTimeMillis()/1000 -> DateTimeOffset; DAO/SecurityConfig/BrokerService red-tolerated.</summary>
public abstract class AbstractPlayerInfoPacket : AionServerPacket
{
    /// <summary>
    /// The maximum number of characters the client can display. The client expects a fixed size text buffer in various packets.
    /// </summary>
    public const int CHARNAME_MAX_LENGTH = 25;

    protected void WritePlayerInfo(PlayerAccountData accPlData, AionConnection con)
    {
        PlayerCommonData pcd = accPlData.GetPlayerCommonData();
        int playerId = pcd.GetPlayerObjId();
        LegionMember legionMember = LegionService.GetInstance().GetLegionMember(pcd);
        PlayerAppearance playerAppearance = accPlData.GetAppearance();
        CharacterBanInfo cbi = GetCharBanInfo(accPlData, con);

        WriteD(playerId);
        WriteS(pcd.GetName(), CHARNAME_MAX_LENGTH);
        WriteD(pcd.GetGender().GetGenderId());
        WriteD(pcd.GetRace().GetRaceId());
        WriteD(pcd.GetPlayerClass().GetClassId());
        WriteD(playerAppearance.GetVoice());
        WriteD(playerAppearance.GetSkinRGB());
        WriteD(playerAppearance.GetHairRGB());
        WriteD(playerAppearance.GetEyeRGB());
        WriteD(playerAppearance.GetLipRGB());
        WriteC(playerAppearance.GetFace());
        WriteC(playerAppearance.GetHair());
        WriteC(playerAppearance.GetDeco());
        WriteC(playerAppearance.GetTattoo());
        WriteC(playerAppearance.GetFaceContour());
        WriteC(playerAppearance.GetExpression());
        WriteC(5);// always 5 o0
        WriteC(playerAppearance.GetJawLine());
        WriteC(playerAppearance.GetForehead());
        WriteC(playerAppearance.GetEyeHeight());
        WriteC(playerAppearance.GetEyeSpace());
        WriteC(playerAppearance.GetEyeWidth());
        WriteC(playerAppearance.GetEyeSize());
        WriteC(playerAppearance.GetEyeShape());
        WriteC(playerAppearance.GetEyeAngle());
        WriteC(playerAppearance.GetBrowHeight());
        WriteC(playerAppearance.GetBrowAngle());
        WriteC(playerAppearance.GetBrowShape());
        WriteC(playerAppearance.GetNose());
        WriteC(playerAppearance.GetNoseBridge());
        WriteC(playerAppearance.GetNoseWidth());
        WriteC(playerAppearance.GetNoseTip());
        WriteC(playerAppearance.GetCheek());
        WriteC(playerAppearance.GetLipHeight());
        WriteC(playerAppearance.GetMouthSize());
        WriteC(playerAppearance.GetLipSize());
        WriteC(playerAppearance.GetSmile());
        WriteC(playerAppearance.GetLipShape());
        WriteC(playerAppearance.GetJawHeigh());
        WriteC(playerAppearance.GetChinJut());
        WriteC(playerAppearance.GetEarShape());
        WriteC(playerAppearance.GetHeadSize());
        // 1.5.x 0x00, shoulderSize, armLength, legLength (BYTE) after HeadSize
        WriteC(playerAppearance.GetNeck());
        WriteC(playerAppearance.GetNeckLength());
        WriteC(playerAppearance.GetShoulderSize());
        WriteC(playerAppearance.GetTorso());
        WriteC(playerAppearance.GetChest());
        WriteC(playerAppearance.GetWaist());
        WriteC(playerAppearance.GetHips());
        WriteC(playerAppearance.GetArmThickness());
        WriteC(playerAppearance.GetHandSize());
        WriteC(playerAppearance.GetLegThickness());
        WriteC(playerAppearance.GetFootSize());
        WriteC(playerAppearance.GetFacialRate());
        WriteC(0x00); // 0x00
        WriteC(playerAppearance.GetArmLength());
        WriteC(playerAppearance.GetLegLength());
        WriteC(playerAppearance.GetShoulders());
        WriteC(playerAppearance.GetFaceShape());
        WriteC(0x00); // always 0 may be acessLevel
        WriteC(0x00); // sometimes 0xC7 (199) for all chars, else 0
        WriteC(0x00); // sometimes 0x04 (4) for all chars, else 0
        WriteF(playerAppearance.GetHeight());
        WriteD(pcd.GetTemplateId());
        WriteD(pcd.GetMapId()); // mapid for preloading map
        WriteF(pcd.GetX());
        WriteF(pcd.GetY());
        WriteF(pcd.GetZ());
        WriteD(pcd.GetHeading());
        WriteH(pcd.GetLevel());
        WriteH(0); // unk 2.5
        WriteD(pcd.GetTitleId());
        WriteD(legionMember != null ? legionMember.GetLegion().GetLegionId() : 0);
        WriteS(legionMember != null ? legionMember.GetLegion().GetName() : null, 40);
        WriteH(legionMember != null ? 1 : 0);
        WriteD(pcd.GetLastOnlineEpochSeconds());
        for (int i = 0; i < 16; i++)
        { // 16 items is always expected by the client...
            PlayerAccountData.VisibleItem item = i < accPlData.GetVisibleItems().Count ? accPlData.GetVisibleItems()[i] : null;
            WriteC(item == null ? 0 : item.SlotType); // 0 = not visible, 1 = default (right-hand) slot, 2 = secondary (left-hand) slot
            WriteD(item == null ? 0 : item.ItemId);
            WriteD(item == null ? 0 : item.GodStoneId);
            WriteDyeInfo(item == null ? null : item.Color);
        }
        WriteD(0);
        WriteD(0);
        WriteD(0); // 4.5
        WriteD(0); // 4.5
        WriteD(0); // 4.5
        WriteD(0); // 4.5
        WriteB(new byte[68]); // 4.7
        WriteD(accPlData.GetDeletionTimeInSeconds());
        WriteH(PlayerSettingsDAO.LoadSettings(playerId).GetDisplay()); // display helmet 0 show, 5 dont show , possible bit operation
        WriteH(0);
        WriteD(0); // total mail count
        WriteD(MailDAO.HaveUnread(playerId) ? 1 : 0); // unread mail count
        WriteD(0); // express mail count
        WriteD(0); // blackcloud mail count
        WriteQ(BrokerService.GetInstance().GetEarnedKinahFromSoldItems(pcd)); // collected money from broker
        WriteD(0);
        WriteD(0);
        WriteD(0);
        WriteD(0);
        WriteD(0);
        WriteD(cbi == null ? 0 : (int)cbi.GetStart()); // startPunishDate
        WriteD(cbi == null ? 0 : (int)cbi.GetEnd()); // endPunishDate
        WriteS(cbi == null ? "" : cbi.GetReason());
    }

    protected void WriteEquippedItems(List<Item> items)
    {
        int mask = 0;
        foreach (Item item in items)
        {
            mask |= item.GetEquipmentSlot();
            // remove sub hand mask bits (sub hand is present on TwoHandeds by default and would produce display bugs)
            if (ItemSlotExtensions.IsTwoHandedWeapon(item.GetEquipmentSlot()))
                mask &= ~ItemSlot.SUB_HAND.GetSlotIdMask();
        }

        WriteD(mask);
        foreach (Item item in items)
        {
            WriteD(item.GetItemSkinTemplate().GetTemplateId());
            WriteD(item.GetGodStoneId());
            WriteDyeInfo(item.GetItemColor());
            WriteH(item.GetItemEnchantParam());
            WriteH(0); // 4.7
        }
    }

    private CharacterBanInfo GetCharBanInfo(PlayerAccountData playerAccountData, AionConnection con)
    {
        CharacterBanInfo cbi = playerAccountData.GetCharBanInfo();
        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000;
        if (cbi != null && nowSeconds >= cbi.GetEnd())
            cbi = null;
        if (cbi == null && SecurityConfig.MULTI_CLIENTING_RESTRICTION_MODE == SecurityConfig.MultiClientingRestrictionMode.SAME_FACTION)
        {
            int cdMinutes = SecurityConfig.MULTI_CLIENTING_FACTION_SWITCH_COOLDOWN_MINUTES;
            if (cdMinutes > 0 && MultiClientingService.CheckForFactionSwitchCooldownTime(playerAccountData.GetPlayerCommonData().GetRace(), con) != null)
            {
                int durationSeconds = 61; // client will send CM_CHARACTER_LIST after this duration to update the ban info (<61s corrupts the ban info)
                cbi = new CharacterBanInfo(nowSeconds, durationSeconds, "\n\n\n " + cdMinutes + " minute cooldown between switching factions\n\n\n\n\n\n\n");
            }
        }
        return cbi;
    }
}
