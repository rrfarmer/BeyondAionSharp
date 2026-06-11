using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services;

namespace Aion.GameServer.Model;

/// <summary>Java parity: model/DialogPage (Rolandas, Neon). Enum with (dialogActionId,id) per member→enum + DialogPageExtensions (static data dict + Id()/static lookups). switch-expression npc-id table preserved; DialogService/QuestEngine/QuestService red-tolerated.</summary>
public enum DialogPage
{
    NULL,
    STIGMA,
    CREATE_LEGION,
    ASK_QUEST_ACCEPT_WINDOW,
    SELECT_QUEST_REWARD_WINDOW1,
    SELECT_QUEST_REWARD_WINDOW2,
    SELECT_QUEST_REWARD_WINDOW3,
    SELECT_QUEST_REWARD_WINDOW4,
    SELECT_QUEST_REWARD_WINDOW5,
    SELECT_QUEST_REWARD_WINDOW6,
    SELECT_QUEST_REWARD_WINDOW7,
    SELECT_QUEST_REWARD_WINDOW8,
    SELECT_QUEST_REWARD_WINDOW9,
    SELECT_QUEST_REWARD_WINDOW10,
    VENDOR,
    RETRIEVE_CHAR_WAREHOUSE,
    DEPOSIT_CHAR_WAREHOUSE,
    RETRIEVE_ACCOUNT_WAREHOUSE,
    DEPOSIT_ACCOUNT_WAREHOUSE,
    MAIL,
    CHANGE_ITEM_SKIN,
    REMOVE_MANASTONE,
    GIVE_ITEM_PROC,
    GATHER_SKILL_LEVELUP,
    LOOT,
    LEGION_WAREHOUSE,
    NO_RIGHT,
    COMBINETASK_WINDOW,
    COMPOUND_WEAPON,
    DECOMPOUND_WEAPON,
    HOUSING_MARKER,
    HOUSING_LIFETIME,
    CHARGE_ITEM,
    CHARGE_ITEM2,
    HOUSING_FRIENDLIST,
    HOUSING_POST,
    HOUSING_AUCTION,
    HOUSING_PAY_RENT,
    HOUSING_KICK,
    HOUSING_CONFIG,
    TOWN_CHALLENGE_TASK,
    ITEM_UPGRADE,
    OPEN_STIGMA_ENCHANT
}

public static class DialogPageExtensions
{
    private readonly struct PageData
    {
        public readonly int Id;
        public readonly int DialogActionId;

        public PageData(int dialogActionId, int id)
        {
            Id = id;
            DialogActionId = dialogActionId;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<DialogPage, PageData> data = new()
    {
        [DialogPage.NULL] = new PageData(DialogAction.NULL, 0),
        [DialogPage.STIGMA] = new PageData(DialogAction.OPEN_STIGMA_WINDOW, 1),
        [DialogPage.CREATE_LEGION] = new PageData(DialogAction.CREATE_LEGION, 2),
        [DialogPage.ASK_QUEST_ACCEPT_WINDOW] = new PageData(0, 4),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW1] = new PageData(0, 5),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW2] = new PageData(0, 6),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW3] = new PageData(0, 7),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW4] = new PageData(0, 8),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW5] = new PageData(0, 45),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW6] = new PageData(0, 46),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW7] = new PageData(0, 47),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW8] = new PageData(0, 48),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW9] = new PageData(0, 49),
        [DialogPage.SELECT_QUEST_REWARD_WINDOW10] = new PageData(0, 50),
        [DialogPage.VENDOR] = new PageData(DialogAction.OPEN_VENDOR, 13),
        [DialogPage.RETRIEVE_CHAR_WAREHOUSE] = new PageData(DialogAction.RETRIEVE_CHAR_WAREHOUSE, 14),
        [DialogPage.DEPOSIT_CHAR_WAREHOUSE] = new PageData(DialogAction.DEPOSIT_CHAR_WAREHOUSE, 26), // open char warehouse
        [DialogPage.RETRIEVE_ACCOUNT_WAREHOUSE] = new PageData(DialogAction.RETRIEVE_ACCOUNT_WAREHOUSE, 16),
        [DialogPage.DEPOSIT_ACCOUNT_WAREHOUSE] = new PageData(DialogAction.DEPOSIT_ACCOUNT_WAREHOUSE, 17),
        [DialogPage.MAIL] = new PageData(DialogAction.OPEN_POSTBOX, 18),
        [DialogPage.CHANGE_ITEM_SKIN] = new PageData(DialogAction.CHANGE_ITEM_SKIN, 19),
        [DialogPage.REMOVE_MANASTONE] = new PageData(DialogAction.REMOVE_ITEM_OPTION, 20),
        [DialogPage.GIVE_ITEM_PROC] = new PageData(DialogAction.GIVE_ITEM_PROC, 21),
        [DialogPage.GATHER_SKILL_LEVELUP] = new PageData(DialogAction.GATHER_SKILL_LEVELUP, 23),
        [DialogPage.LOOT] = new PageData(0, 24),
        [DialogPage.LEGION_WAREHOUSE] = new PageData(DialogAction.OPEN_LEGION_WAREHOUSE, 25),
        [DialogPage.NO_RIGHT] = new PageData(0, 27),
        [DialogPage.COMBINETASK_WINDOW] = new PageData(DialogAction.COMBINE_TASK, 28),
        [DialogPage.COMPOUND_WEAPON] = new PageData(DialogAction.COMPOUND_WEAPON, 29),
        [DialogPage.DECOMPOUND_WEAPON] = new PageData(DialogAction.DECOMPOUND_WEAPON, 30),
        [DialogPage.HOUSING_MARKER] = new PageData(DialogAction.HOUSING_BUILD, 32), // housing build
        [DialogPage.HOUSING_LIFETIME] = new PageData(DialogAction.HOUSING_DESTRUCT, 33), // housing destruct
        [DialogPage.CHARGE_ITEM] = new PageData(DialogAction.CHARGE_ITEM_SINGLE, 35), // Actually, two choices
        [DialogPage.CHARGE_ITEM2] = new PageData(DialogAction.CHARGE_ITEM_SINGLE2, 42),
        [DialogPage.HOUSING_FRIENDLIST] = new PageData(DialogAction.HOUSING_BUDDY_LIST, 36),
        [DialogPage.HOUSING_POST] = new PageData(0, 37), // Unknown
        [DialogPage.HOUSING_AUCTION] = new PageData(DialogAction.HOUSING_PERSONAL_AUCTION, 38),
        [DialogPage.HOUSING_PAY_RENT] = new PageData(DialogAction.HOUSING_PAY_RENT, 39),
        [DialogPage.HOUSING_KICK] = new PageData(DialogAction.HOUSING_KICK, 40),
        [DialogPage.HOUSING_CONFIG] = new PageData(DialogAction.HOUSING_CONFIG, 41),
        [DialogPage.TOWN_CHALLENGE_TASK] = new PageData(DialogAction.TOWN_CHALLENGE, 43),
        [DialogPage.ITEM_UPGRADE] = new PageData(DialogAction.ITEM_UPGRADE, 52),
        [DialogPage.OPEN_STIGMA_ENCHANT] = new PageData(DialogAction.OPEN_STIGMA_ENCHANT, 53),
    };

    public static int Id(this DialogPage self)
    {
        return data[self].Id;
    }

    public static DialogPage GetByActionId(int dialogActionId)
    {
        foreach (DialogPage page in System.Enum.GetValues<DialogPage>())
        {
            if (data[page].DialogActionId == dialogActionId)
                return page;
        }
        return DialogPage.NULL;
    }

    public static DialogPage GetRewardPageByIndex(int? rewardIndex)
    {
        if (rewardIndex != null)
        {
            switch (rewardIndex)
            {
                case 0:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW1;
                case 1:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW2;
                case 2:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW3;
                case 3:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW4;
                case 4:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW5;
                case 5:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW6;
                case 6:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW7;
                case 7:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW8;
                case 8:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW9;
                case 9:
                    return DialogPage.SELECT_QUEST_REWARD_WINDOW10;
            }
        }
        return DialogPage.NULL;
    }

    public static int GetStartPageId(Npc npc, Player player)
    {
        TalkInfo talkInfo = npc.GetObjectTemplate().GetTalkInfo();
        if (talkInfo == null || !talkInfo.IsDialogNpc() && talkInfo.GetFuncDialogIds() == null)
            return 0; // HTML_PAGE_NULL (npc has no conversation or function dialog)
        if (!DialogService.IsInteractionAllowed(player, npc))
            return 1011; // HTML_PAGE_SELECT1 (npc will say you are not allowed to use their function)
        bool isFunctionalNpc = talkInfo.GetFuncDialogIds() != null;
        if (isFunctionalNpc || HasQuestInteraction(player, npc))
            return 10; // HTML_PAGE_SELECT_QUEST (npc will offer their function or quests)
        if (player.GetCommonData().IsDaeva() && HasAlternativeDialogAfterAscension(npc))
            return 1352; // HTML_PAGE_SELECT2
        return 1011; // HTML_PAGE_SELECT1 (default dialog)
    }

    private static bool HasAlternativeDialogAfterAscension(Npc npc)
    {
        //@formatter:off
        return npc.GetNpcId() switch
        {
            // Poeta
            203049 or // elpis
            203050 or // kales
            203058 or // asteros
            203059 or // polinia
            203060 or // mune
            203066 or // toleo
            203072 or // feira
            203074 or // pranoa
            203075 or // namus
            203079 or // melpone
            730007 or // tree_move_noah
            790001 or // pernos
            // Ishalgen
            203500 or // ask
            203501 or // guheitun
            203502 or // vanar
            203503 or // gurt
            203504 or // vandarnt
            203507 or // galar
            203511 or // huggin
            203516 or // ulgorn
            203517 or // tobu
            203518 or // boromer
            203519 or // nobekk
            203522 or // kaindal
            203523 or // glifko
            203524 or // megin
            203525 or // sraht
            203531 or // negi
            203532 or // df_fisher
            203533 or // motgar
            203534 or // davi
            203535 or // kard
            203539 or // derot
            203540 or // mijou
            203541 or // lidun
            203543 or // alfrigh
            203544 or // devalin
            203546 or // skuld
            203547 or // erre
            203548 or // djuefene
            203549 or // tanmarn
            203550 or // muninn
            203552 or // nostla
            203589 or // npc_moomoobong
            203590 or // lycan_messenger
            790002 or // verdandi
            790003 // urd
                => true,
            _ => false,
        };
        //@formatter:on
    }

    private static bool HasQuestInteraction(Player player, Npc npc)
    {
        QuestNpc questNpc = QuestEngine.GetInstance().GetQuestNpc(npc.GetNpcId());
        if (questNpc == null)
            return false;
        return questNpc.GetOnQuestStart().Any(startableQuest => QuestService.CheckStartConditions(player, startableQuest, false))
            || player.GetQuestStateList().GetUncompletedQuests().Any(activeQuest => questNpc.GetOnTalkEvent().Contains(activeQuest.GetQuestId()));
    }
}
