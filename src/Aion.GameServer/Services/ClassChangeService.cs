using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/ClassChangeService (ATracer, sweetkr, Neon).</summary>
public class ClassChangeService
{
    public static void ShowClassChangeDialog(Player player)
    {
        PlayerClass playerClass = player.GetPlayerClass();
        Race playerRace = player.GetRace();
        if (player.GetLevel() >= 9 && playerClass.IsStartingClass())
            PacketSendUtility.SendPacket(player,
                new SmDialogWindow(0, GetClassSelectionDialogPageId(playerRace, playerClass), playerRace == Race.ELYOS ? 1006 : 2008));
    }

    public static void ChangeClassToSelection(Player player, int dialogActionId)
    {
        SetClass(player, GetSelectedPlayerClass(player.GetRace(), dialogActionId), true, true);
        PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0)); // close dialog window
    }

    public static void CompleteAscensionQuest(Player player)
    {
        int questId = player.GetRace() == Race.ELYOS ? 1006 : 2008;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            qs = new QuestState(questId, QuestStatus.COMPLETE);
            player.GetQuestStateList().AddQuest(questId, qs);
            PacketSendUtility.SendPacket(player, SmQuestAction.Add(qs));
        }
        else
        {
            qs.SetStatus(QuestStatus.COMPLETE);
        }
        qs.SetQuestVar(0);
        qs.SetRewardGroup(0);
        PacketSendUtility.SendPacket(player, SmQuestAction.Update(qs));
    }

    public static bool SetClass(Player player, PlayerClass newClass)
    {
        return SetClass(player, newClass, true, false);
    }

    public static bool SetClass(Player player, PlayerClass? newClass, bool validate, bool updateDaevaStatus)
    {
        if (newClass == null)
            return false;

        PlayerClass nc = newClass.Value;

        if (validate)
        {
            PlayerClass oldClass = player.GetPlayerClass();
            if (!oldClass.IsStartingClass())
            {
                PacketSendUtility.SendMessage(player, "You already switched class");
                return false;
            }
            byte id = oldClass.GetClassId(); // starting class ID +1/+2 equals valid subclass ID
            if (oldClass == nc || nc.GetClassId() <= id || nc.GetClassId() > id + 2)
            {
                PacketSendUtility.SendMessage(player, "Invalid class chosen");
                return false;
            }
        }

        player.GetCommonData().SetPlayerClass(nc);
        player.GetGameStats().UpdateStatsTemplate();
        player.GetController().UpgradePlayer();
        PacketSendUtility.BroadcastPacket(player, new SmActionAnimation(player.GetObjectId(), SmActionAnimation.ClassChange, player.GetLevel()), true);
        PacketSendUtility.BroadcastPacket(player, new SmPlayerInfo(player));
        SkillLearnService.LearnNewSkills(player, 9, player.GetLevel());

        if (updateDaevaStatus)
        {
            if (!nc.IsStartingClass())
            {
                CompleteAscensionQuest(player);
                player.GetCommonData().UpdateDaeva();
            }
            else
            {
                player.GetCommonData().SetDaeva(false);
            }
        }
        return true;
    }

    public static int GetClassSelectionDialogPageId(Race playerRace, PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.WARRIOR:
                return playerRace == Race.ELYOS ? 2375 : 3057;
            case PlayerClass.SCOUT:
                return playerRace == Race.ELYOS ? 2716 : 3398;
            case PlayerClass.MAGE:
                return playerRace == Race.ELYOS ? 3057 : 3739;
            case PlayerClass.PRIEST:
                return playerRace == Race.ELYOS ? 3398 : 4080;
            case PlayerClass.ENGINEER:
                return playerRace == Race.ELYOS ? 3739 : 3569;
            case PlayerClass.ARTIST:
                return playerRace == Race.ELYOS ? 4080 : 3910;
            default:
                return 0;
        }
    }

    public static PlayerClass? GetSelectedPlayerClass(Race race, int dialogActionId)
    {
        switch (race)
        {
            case Race.ELYOS:
                switch (dialogActionId)
                {
                    case DialogAction.SELECT5_1:
                        return PlayerClass.GLADIATOR;
                    case DialogAction.SELECT5_2:
                        return PlayerClass.TEMPLAR;
                    case DialogAction.SELECT6_1:
                        return PlayerClass.ASSASSIN;
                    case DialogAction.SELECT6_2:
                        return PlayerClass.RANGER;
                    case DialogAction.SELECT7_1:
                        return PlayerClass.SORCERER;
                    case DialogAction.SELECT7_2:
                        return PlayerClass.SPIRIT_MASTER;
                    case DialogAction.SELECT8_1:
                        return PlayerClass.CLERIC;
                    case DialogAction.SELECT8_2:
                        return PlayerClass.CHANTER;
                    case DialogAction.SELECT9_1:
                        return PlayerClass.GUNNER;
                    case DialogAction.SELECT9_2:
                        return PlayerClass.RIDER;
                    case DialogAction.SELECT10_1:
                        return PlayerClass.BARD;
                }
                break;
            case Race.ASMODIANS:
                switch (dialogActionId)
                {
                    case DialogAction.SELECT7_1:
                        return PlayerClass.GLADIATOR;
                    case DialogAction.SELECT7_2:
                        return PlayerClass.TEMPLAR;
                    case DialogAction.SELECT8_1:
                        return PlayerClass.ASSASSIN;
                    case DialogAction.SELECT8_2:
                        return PlayerClass.RANGER;
                    case DialogAction.SELECT9_1:
                        return PlayerClass.SORCERER;
                    case DialogAction.SELECT9_2:
                        return PlayerClass.SPIRIT_MASTER;
                    case DialogAction.SELECT10_1:
                        return PlayerClass.CLERIC;
                    case DialogAction.SELECT10_2:
                        return PlayerClass.CHANTER;
                    case DialogAction.SELECT8_3_1:
                        return PlayerClass.GUNNER;
                    case DialogAction.SELECT8_3_2:
                        return PlayerClass.RIDER;
                    case DialogAction.SELECT9_3_1:
                        return PlayerClass.BARD;
                }
                break;
        }
        return null;
    }
}
