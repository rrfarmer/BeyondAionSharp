using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Emotion;
using Aion.GameServer.Model.GameObjects.Players.Motion;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.GameObjects.Players.Title;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using PersistentState = Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;

namespace Aion.GameServer.Services.Transfers;

/// <summary>Java parity: services/transfers/CMT_CHARACTER_INFORMATION (KID) extends BaseClientPacket&lt;AionConnection&gt;. Deserializes a transferred character blob (common/appearance/position, items, emotes/motions/macros/npc-factions/pets/titles, settings/abyss, skills, recipes, quests) into a new Player honoring PlayerTransferConfig allow-flags. ByteBuffer read*/BaseClientPacket red-tolerated; currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; Integer itemColor==-1->null (int?); new Timestamp(ms)->DateTimeOffset.FromUnixTimeMilliseconds; enum.valueOf(s)->Enum.Parse; Java byte->sbyte; String.format->string.Format; protected ctor->public (cross-class instantiation). Many model/DAO types red-tolerated.</summary>
public class CMT_CHARACTER_INFORMATION : BaseClientPacket<AionConnection>
{
    public CMT_CHARACTER_INFORMATION(byte[] byteBuffer)
        : base(byteBuffer, 0)
    {
    }

    public override void Run()
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
    }

    public Player ReadInfo(string name, int targetAccount, string accountName, List<int> rsList, ILogger textLog)
    {
        long st = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        PlayerCommonData playerCommonData = new PlayerCommonData(IDFactory.GetInstance().NextId());
        playerCommonData.SetName(name);
        // read common data
        playerCommonData.SetPlayerClass(PlayerClassExtensions.GetPlayerClassById((byte)ReadD()));
        playerCommonData.SetExp(ReadQ());
        playerCommonData.SetRace(ReadD() == 0 ? Race.ELYOS : Race.ASMODIANS);
        playerCommonData.SetGender(ReadD() == 0 ? Gender.MALE : Gender.FEMALE);
        playerCommonData.SetTitleId(ReadD());
        playerCommonData.SetDp(ReadD());
        playerCommonData.SetQuestExpands(ReadD());
        playerCommonData.SetNpcExpands(ReadD());
        playerCommonData.SetItemExpands(ReadD());
        playerCommonData.SetWhNpcExpands(ReadD());

        PlayerAppearance playerAppearance = new PlayerAppearance();
        playerAppearance.SetSkinRGB(ReadD());
        playerAppearance.SetHairRGB(ReadD());
        playerAppearance.SetEyeRGB(ReadD());
        playerAppearance.SetLipRGB(ReadD());
        playerAppearance.SetFace(ReadUC());
        playerAppearance.SetHair(ReadUC());
        playerAppearance.SetDeco(ReadUC());
        playerAppearance.SetTattoo(ReadUC());
        playerAppearance.SetFaceContour(ReadUC());
        playerAppearance.SetExpression(ReadUC());
        playerAppearance.SetJawLine(ReadUC());
        playerAppearance.SetForehead(ReadUC());
        playerAppearance.SetEyeHeight(ReadUC());
        playerAppearance.SetEyeSpace(ReadUC());
        playerAppearance.SetEyeWidth(ReadUC());
        playerAppearance.SetEyeSize(ReadUC());
        playerAppearance.SetEyeShape(ReadUC());
        playerAppearance.SetEyeAngle(ReadUC());
        playerAppearance.SetBrowHeight(ReadUC());
        playerAppearance.SetBrowAngle(ReadUC());
        playerAppearance.SetBrowShape(ReadUC());
        playerAppearance.SetNose(ReadUC());
        playerAppearance.SetNoseBridge(ReadUC());
        playerAppearance.SetNoseWidth(ReadUC());
        playerAppearance.SetNoseTip(ReadUC());
        playerAppearance.SetCheek(ReadUC());
        playerAppearance.SetLipHeight(ReadUC());
        playerAppearance.SetMouthSize(ReadUC());
        playerAppearance.SetLipSize(ReadUC());
        playerAppearance.SetSmile(ReadUC());
        playerAppearance.SetLipShape(ReadUC());
        playerAppearance.SetJawHeigh(ReadUC());
        playerAppearance.SetChinJut(ReadUC());
        playerAppearance.SetEarShape(ReadUC());
        playerAppearance.SetHeadSize(ReadUC());
        playerAppearance.SetNeck(ReadUC());
        playerAppearance.SetNeckLength(ReadUC());
        playerAppearance.SetShoulderSize(ReadUC());
        playerAppearance.SetTorso(ReadUC());
        playerAppearance.SetChest(ReadUC());
        playerAppearance.SetWaist(ReadUC());
        playerAppearance.SetHips(ReadUC());
        playerAppearance.SetArmThickness(ReadUC());
        playerAppearance.SetHandSize(ReadUC());
        playerAppearance.SetLegThickness(ReadUC());
        playerAppearance.SetFootSize(ReadUC());
        playerAppearance.SetFacialRate(ReadUC());
        playerAppearance.SetArmLength(ReadUC());
        playerAppearance.SetLegLength(ReadUC());
        playerAppearance.SetShoulders(ReadUC());
        playerAppearance.SetFaceShape(ReadUC());
        playerAppearance.SetVoice(ReadUC());
        playerAppearance.SetHeight(ReadF());

        PlayerAccountData accPlData = new PlayerAccountData(playerCommonData, playerAppearance);
        Account account = AccountService.LoadAccount(targetAccount);
        account.SetName(accountName);
        Player player = PlayerService.NewPlayer(accPlData, account);
        float x = ReadF();
        float y = ReadF();
        float z = ReadF();
        sbyte h = (sbyte)ReadC();
        int worldId = ReadD();
        WorldPosition pos = Aion.GameServer.World.World.GetInstance().CreatePosition(worldId, x, y, z, (byte)h, 1);
        player.SetPosition(pos);

        if (!PlayerService.StoreNewPlayer(player, accountName, targetAccount))
        {
            textLog.LogInformation("failed to store new player to " + accountName);
            IDFactory.GetInstance().ReleaseId(playerCommonData.GetPlayerObjId());
            return null;
        }
        // read items data
        int cnt = ReadD();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int a = 0; a < cnt; a++)
        { // inventory
            int objIdOld = ReadD();
            int itemId = ReadD();
            long itemCnt = ReadQ();
            int? itemColor = ReadD();
            if (itemColor == -1)
                itemColor = null;

            string itemCreator = ReadS();
            int itemExpireTime = ReadD();
            int itemActivationCnt = ReadD();
            bool itemEquipped = ReadC() == 1;

            bool itemSoulBound = ReadC() == 1;
            long equipSlot = ReadQ();
            int location = ReadD();
            int enchant = ReadD();
            int enchantBonus = ReadD();

            int skinId = ReadD();
            int fusionId = ReadD();
            int optSocket = ReadD();
            int optFusion = ReadD();

            int charge = ReadD();
            List<int[]> manastones = new List<int[]>(), fusions = new List<int[]>();
            sbyte len = (sbyte)ReadC();
            for (sbyte b = 0; b < len; b++)
            {
                manastones.Add(new int[] { ReadD(), ReadD() });
            }
            len = (sbyte)ReadC();
            for (sbyte b = 0; b < len; b++)
            {
                fusions.Add(new int[] { ReadD(), ReadD() });
            }
            int godstone = ReadD();
            int colorExpires = ReadD();
            int tuneCount = ReadD();
            int bonusStatsId = ReadD();
            int fusionedItemBonusStatsId = ReadD();
            int tempering = ReadD();
            int packCount = ReadD();
            bool itemAmplified = ReadUC() == 1;
            int buffSkill = ReadUH();
            if (!(location == StorageType.CUBE.GetId() && PlayerTransferConfig.ALLOW_INV
                || location == StorageType.REGULAR_WAREHOUSE.GetId() && PlayerTransferConfig.ALLOW_WAREHOUSE))
            {
                continue;
            }
            ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(itemId);
            if (template == null)
            {
                textLog.LogWarning("(accId=" + targetAccount + ") item with id " + itemId + " was not found in templates");
                continue;
            }

            if (template.IsStigma() && !PlayerTransferConfig.ALLOW_STIGMA)
            {
                continue;
            }

            int newId = IDFactory.GetInstance().NextId();
            // bonus probably is lost, don't know [RR]
            // dye expiration is lost
            // plume Bonus is lost
            Item item = new Item(newId, itemId, itemCnt, itemColor, colorExpires, itemCreator, itemExpireTime, itemActivationCnt, itemEquipped,
                itemSoulBound, equipSlot, location, enchant, enchantBonus, skinId, fusionId, optSocket, optFusion, charge, tuneCount, bonusStatsId,
                fusionedItemBonusStatsId, tempering, packCount, itemAmplified, buffSkill, 0);
            if (manastones.Count > 0)
                foreach (int[] stone in manastones)
                    Aion.GameServer.Services.Items.ItemSocketService.AddManaStone(item, stone[0], stone[1], false);

            if (fusions.Count > 0)
                foreach (int[] stone in fusions)
                    Aion.GameServer.Services.Items.ItemSocketService.AddManaStone(item, stone[0], stone[1], true);

            if (godstone != 0)
                item.AddGodStone(godstone);

            sb.Append("\n(old objId=").Append(objIdOld).Append(") -> ").Append(item);
            item.SetPersistentState(PersistentState.NEW);
            player.GetInventory().Add_CharacterTransfer(item);
        }
        InventoryDAO.Store(player);

        textLog.LogInformation(sb.ToString());

        // read data
        cnt = ReadD();
        textLog.LogInformation("EmotionList:" + cnt);
        player.SetEmotions(new EmotionList(player));
        for (int a = 0; a < cnt; a++)
        { // emotes
            int id = ReadD(), remainTime = ReadD();

            if (PlayerTransferConfig.ALLOW_EMOTIONS)
                player.GetEmotions().Add(id, remainTime, true);
        }

        cnt = ReadD();
        textLog.LogInformation("MotionList:" + cnt);
        player.SetMotions(new MotionList(player));
        for (int i = 0; i < cnt; i++)
        { // motions
            int id = ReadD(), expiryTime = ReadD();
            bool active = ReadC() == 1;

            if (PlayerTransferConfig.ALLOW_MOTIONS)
                player.GetMotions().Add(new global::Aion.GameServer.Model.GameObjects.Players.Motion.Motion(id, expiryTime, active), true);
        }

        cnt = ReadD();
        textLog.LogInformation("Macros:" + cnt);
        player.SetMacros(new Macros());
        for (int a = 0; a < cnt; a++)
        { // macros
            int id = ReadD();
            string xml = ReadS();

            if (PlayerTransferConfig.ALLOW_MACRO)
                PlayerService.AddMacro(player, id, xml);
        }

        cnt = ReadD();
        textLog.LogInformation("NpcFactions:" + cnt);
        player.SetNpcFactions(new NpcFactions(player));
        for (int a = 0; a < cnt; a++)
        { // npc factions
            int id = ReadD(), time = ReadD();
            bool active = ReadC() == 1;
            string state = ReadS();
            int questId = ReadD();

            if (PlayerTransferConfig.ALLOW_NPCFACTIONS)
                player.GetNpcFactions().AddNpcFaction(new NpcFaction(id, time, active, Enum.Parse<ENpcFactionQuestState>(state), questId));
        }
        if (cnt > 0 && PlayerTransferConfig.ALLOW_NPCFACTIONS)
            PlayerNpcFactionsDAO.StoreNpcFactions(player);

        cnt = ReadD();
        textLog.LogInformation("Pets:" + cnt);
        for (int i = 0; i < cnt; i++)
        { // pets
            int petId = ReadD();
            int decorationId = ReadD();
            long bday = ReadQ();
            string petname = ReadS();
            int expiryTime = ReadD();

            if (PlayerTransferConfig.ALLOW_PETS)
            {
                if (bday == 0)
                    bday = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                player.GetPetList().AddPet(player, petId, decorationId, bday, petname, expiryTime);
            }
        }

        cnt = ReadD();
        textLog.LogInformation("TitleList:" + cnt);
        player.SetTitleList(new TitleList());
        for (int a = 0; a < cnt; a++)
        { // titles
            int id = ReadD(), remainTime = ReadD();

            if (PlayerTransferConfig.ALLOW_TITLES)
                player.GetTitleList().AddEntry(id, remainTime);
        }
        if (cnt > 0 && PlayerTransferConfig.ALLOW_TITLES)
            foreach (Title t in player.GetTitleList().GetTitles())
            {
                PlayerTitleListDAO.StoreTitles(player, t);
            }

        string[] posBind;
        switch (player.GetRace())
        {
            case Race.ELYOS:
                posBind = PlayerTransferConfig.BIND_ELYOS.Split(' ');
                break;
            default:
                posBind = PlayerTransferConfig.BIND_ASMO.Split(' ');
                break;
        }

        player.SetBindPoint(new BindPointPosition(int.Parse(posBind[0]), float.Parse(posBind[1]), float.Parse(posBind[2]),
            float.Parse(posBind[3]), byte.Parse(posBind[4])));
        PlayerBindPointDAO.Store(player);

        int uilen = ReadD(), shortlen = ReadD();
        byte[] ui = ReadB(uilen), sc = ReadB(shortlen);
        int deny = ReadD(), penalty = ReadD();
        player.SetPlayerSettings(new global::Aion.GameServer.Model.GameObjects.Players.PlayerSettings(uilen > 0 ? ui : null, shortlen > 0 ? sc : null, null, deny, penalty));
        player.SetAbyssRank(new AbyssRank(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0));

        // read skill data
        cnt = ReadD();
        textLog.LogInformation("PlayerSkillList:" + cnt);
        player.SetSkillList(new PlayerSkillList());
        bool rsCheck = rsList.Count > 0;
        for (int a = 0; a < cnt; a++)
        { // skills
            int skillId = ReadD();
            int skillLvl = ReadD();

            if (rsCheck && rsList.Contains(skillId))
                continue;

            SkillTemplate temp = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            if (temp == null)
            {
                textLog.LogError(string.Format("null skillid:{0} name:{1}", skillId, name));
                continue;
            }

            if (!PlayerTransferConfig.ALLOW_SKILLS)
            {
                if (temp.IsPassive())
                    player.GetSkillList().AddSkill(player, skillId, skillLvl);
            }
            else
                player.GetSkillList().AddSkill(player, skillId, skillLvl);
        }

        // read recipe data
        cnt = ReadD();
        textLog.LogInformation("RecipeList:" + cnt);
        player.SetRecipeList(new RecipeList());
        for (int a = 0; a < cnt; a++)
        { // recipes
            int recipeId = ReadD();

            if (PlayerTransferConfig.ALLOW_RECIPES)
                player.GetRecipeList().AddRecipe(player, recipeId);
        }

        // read quest data
        cnt = ReadD();
        textLog.LogInformation("QuestStateList:" + cnt);
        player.SetQuestStateList(new QuestStateList());
        for (int a = 0; a < cnt; a++)
        { // quests
            int questId = ReadD();
            string status = ReadS();
            int qvars = ReadD(), completeCount = ReadD(), reward = ReadD();
            DateTime completeTime = DateTimeOffset.FromUnixTimeMilliseconds(ReadQ()).UtcDateTime;
            DateTime nextRepeatTime = DateTimeOffset.FromUnixTimeMilliseconds(ReadQ()).UtcDateTime;
            int flags = ReadD();

            if (PlayerTransferConfig.ALLOW_QUESTS)
            {
                player.GetQuestStateList().AddQuest(questId, new QuestState(questId, Enum.Parse<QuestStatus>(status), qvars, flags, completeCount, nextRepeatTime,
                    reward == -1 ? (int?)null : reward, completeTime));
            }
        }

        PlayerService.StorePlayer(player);
        textLog.LogInformation("finished in " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - st) + " ms");
        return player;
    }
}
