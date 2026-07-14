using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SysMail. Author: xTz.</summary>
public class SysMail : AdminCommand
{
    public SysMail()
        : base("sysmail")
    {
    }

    private enum RecipientType
    {
        ELYOS,
        ASMO,
        ALL,
        PLAYER,
    }

    // Java parity: RecipientType.isAllowed(Race) (C# enums can't carry methods).
    private static bool IsAllowed(RecipientType type, Race race)
    {
        switch (type)
        {
            case RecipientType.ELYOS:
                return race == Race.ELYOS;
            case RecipientType.ASMO:
                return race == Race.ASMODIANS;
            case RecipientType.ALL:
                return race == Race.ELYOS || race == Race.ASMODIANS;
            default:
                return false;
        }
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 5)
        {
            Info(admin, null);
            return;
        }

        string[] paramValues = new string[paramsArr.Length];
        System.Array.Copy(paramsArr, 0, paramValues, 0, paramsArr.Length);

        RecipientType? recipientType = null;
        string sender;
        string recipient = null;

        if (paramValues[0].StartsWith("$$") || paramValues[0].StartsWith("%"))
        {
            if (paramsArr.Length < 6)
            {
                Info(admin, null);
                return;
            }
            sender = paramValues[0];
            paramValues = new string[paramsArr.Length - 1];
            System.Array.Copy(paramsArr, 1, paramValues, 0, paramsArr.Length - 1);
        }
        else
        {
            sender = "Admin";
        }

        if (paramValues[0].StartsWith("@"))
        {
            if ("@all".StartsWith(paramValues[0]))
                recipientType = RecipientType.ALL;
            else if ("@elyos".StartsWith(paramValues[0]))
                recipientType = RecipientType.ELYOS;
            else if ("@asmodians".StartsWith(paramValues[0]))
                recipientType = RecipientType.ASMO;
            else
            {
                PacketSendUtility.SendMessage(admin, "Recipient must be Player name, @all, @elyos or @asmodians.");
                return;
            }
        }
        else
        {
            recipientType = RecipientType.PLAYER;
            recipient = Util.ConvertName(paramValues[0]);
        }

        int item = 0, count = 0, kinah = 0;
        LetterType letterType;

        if (!TryParseInt(paramValues[2], out item) || !TryParseInt(paramValues[3], out count) || !TryParseInt(paramValues[4], out kinah)
            || !TryParseInt(paramValues[1], out int letterTypeId))
        {
            PacketSendUtility.SendMessage(admin, "<Regular|Blackcloud|Express> <Item|Count|Kinah> value must be an integer.");
            return;
        }
        letterType = LetterTypeExtensions.GetLetterTypeById(letterTypeId);

        if (letterType == LetterType.BLACKCLOUD)
            sender = "$$CASH_ITEM_MAIL";

        bool express = CheckExpress(admin, item, count, kinah, recipient, recipientType, letterType);
        if (!express)
            return;

        if (item <= 0)
            item = 0;

        if (count <= 0)
            count = -1;

        string title = "System Mail";
        string message = " ";

        if (paramValues.Length > 5)
        {
            string[] words = new string[paramValues.Length - 5];
            System.Array.Copy(paramValues, 5, words, 0, words.Length);
            string[] outText = new string[1];
            int wordCount = ExtractText(words, outText);
            if (wordCount > 0)
            {
                title = outText[0];
                string[] msgWords = new string[words.Length - wordCount];
                System.Array.Copy(words, wordCount, msgWords, 0, msgWords.Length);
                wordCount = ExtractText(msgWords, outText);
                if (wordCount > 0)
                    message = outText[0];
            }
        }

        if (recipientType == RecipientType.PLAYER)
        {
            if (letterType == LetterType.BLACKCLOUD)
                MailFormatter.SendBlackCloudMail(recipient, item, count);
            else
                SystemMailService.SendMail(sender, recipient, title, message, item, count, kinah, letterType);
        }
        else
        {
            foreach (Player player in World.World.GetInstance().GetAllPlayers())
            {
                if (IsAllowed(recipientType.Value, player.GetRace()))
                {
                    if (letterType == LetterType.BLACKCLOUD)
                        MailFormatter.SendBlackCloudMail(player.GetName(), item, count);
                    else
                        SystemMailService.SendMail(sender, player.GetName(), title, message, item, count, kinah, letterType);
                }
            }
        }

        if (item != 0)
        {
            PacketSendUtility.SendMessage(admin, "You send to " + recipientType + (recipientType == RecipientType.PLAYER ? " " + recipient : "") + "\n"
                + "[item:" + item + "] Count:" + count + " Kinah:" + kinah + "\n" + "Letter send successfully.");
        }
        else if (kinah > 0)
        {
            PacketSendUtility.SendMessage(admin, "You send to " + recipientType + (recipientType == RecipientType.PLAYER ? " " + recipient : "") + "\n"
                + " Kinah:" + kinah + "\n" + "Letter send successfully.");
        }
    }

    private int ExtractText(string[] words, string[] outText)
    {
        if (words.Length == 0 || outText.Length == 0)
            return 0;

        if (!words[0].StartsWith("|"))
            return 0;

        int wordCount = 1;

        string enclosedText = words[0].Substring(1);
        if (enclosedText.EndsWith("|"))
        {
            outText[0] = enclosedText.Substring(0, enclosedText.Length - 1);
        }
        else
        {
            List<string> titleWords = new List<string>();
            titleWords.Add(enclosedText);
            for (; wordCount < words.Length; wordCount++)
            {
                string word = words[wordCount];
                if (word.EndsWith("|"))
                {
                    word = word.Substring(0, word.Length - 1);
                    titleWords.Add(word);
                    wordCount++;
                    break;
                }
                else
                    titleWords.Add(word);
            }

            outText[0] = string.Join(" ", titleWords);
        }

        return wordCount;
    }

    private static bool CheckExpress(Player admin, int item, int count, int kinah, string recipient, RecipientType? recipientType,
        LetterType letterType)
    {
        bool shouldExpress = false;

        if (recipientType == null)
        {
            PacketSendUtility.SendMessage(admin, "Please insert Recipient Type.\n" + "Recipient = player, @all, @elyos or @asmodians");
            return false;
        }
        else if (recipientType == RecipientType.PLAYER)
        {
            if (letterType == LetterType.NORMAL)
            {
                if (!PlayerDAO.IsNameUsed(recipient))
                {
                    PacketSendUtility.SendMessage(admin, "Could not find a Recipient by that name.");
                    return false;
                }
                shouldExpress = true;
            }
            else if (letterType == LetterType.EXPRESS)
            {
                if (World.World.GetInstance().GetPlayer(recipient) == null)
                {
                    PacketSendUtility.SendMessage(admin, "This Recipient is offline.");
                    return false;
                }
                shouldExpress = true;
            }
            else
            { // Black cloud
                shouldExpress = World.World.GetInstance().GetPlayer(recipient) != null;
            }
        }
        else
        {
            shouldExpress = letterType != LetterType.NORMAL;
        }

        if (item == 0 && count != 0)
        {
            PacketSendUtility.SendMessage(admin, "Please insert Item Id..");
            return false;
        }

        if (count == 0 && item != 0)
        {
            PacketSendUtility.SendMessage(admin, "Please insert Item Count.");
            return false;
        }

        if (count <= 0 && item <= 0 && kinah <= 0)
        {
            PacketSendUtility.SendMessage(admin, "Parameters <Item> <Count> <Kinah> are icorrect.");
            return false;
        }

        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(item);
        if (item != 0)
        {
            if (itemTemplate == null)
            {
                PacketSendUtility.SendMessage(admin, "Item id is incorrect: " + item);
                return false;
            }
            long maxStackCount = itemTemplate.GetMaxStackCount();
            if (count > maxStackCount && maxStackCount != 0)
            {
                PacketSendUtility.SendMessage(admin, "Please insert correct Item Count.");
                return false;
            }
        }

        if (kinah < 0)
        {
            PacketSendUtility.SendMessage(admin, "Kinah value must be >= 0.");
            return false;
        }
        else if (kinah > 0 && letterType == LetterType.BLACKCLOUD)
        {
            PacketSendUtility.SendMessage(admin, "Kinah attachment are not for black cloud letters!");
            return false;
        }
        return shouldExpress;
    }

    // Java parity: info(Player, String) override. No base virtual exists in C#; ported as a private helper invoked where Java calls info().
    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "No parameters detected.\n"
            + "Please use //sysmail [%|$$<Sender>] <Recipient> <Regular|Blackcloud|Express> <Item> <Count> <Kinah> [|Title|] [|Message|]\n"
            + "Sender name must start with % or $$. Can be ommitted.\n" + "Regular mail type is 0, Express mail type is 1, Blackcloud type is 2.\n"
            + "If parameters (Item, Count) = 0 than the item will not be send\n" + "If parameters (Kinah) = 0 not send Kinah\n"
            + "Recipient = Player name, @all, @elyos or @asmodians\n" + "Optional Title and Message must be enclosed within pipe chars");
    }
}
