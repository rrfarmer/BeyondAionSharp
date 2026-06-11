using System;
using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Services.Reward;

/// <summary>Java parity: services/reward/VeteranRewardService (Neon) — monthly veteran rewards (60 fixed months + random 61+). Singleton (SingletonHolder); static init→static ctor populating rewards[60] + randomRewards; ChronoUnit.MONTHS.between→MonthsBetween helper (complete months); ServerTime.now/atDate/ofEpochMilli→DateTimeOffset; Rnd.nextInt→Rnd.NextInt; list.remove(idx)→RemoveAt; mailbox overflow guard; SystemMailService.sendMail LetterType.BLACKCLOUD. VeteranRewardDAO/RewardItem red-tolerated.</summary>
public sealed class VeteranRewardService
{
    private static readonly List<List<RewardItem>> rewards = new List<List<RewardItem>>();

    private static readonly List<RewardItem> randomRewards = new List<RewardItem>();

    private const int RANDOM_ITEMS_PER_MONTH = 4;

    static VeteranRewardService()
    {
        for (int i = 0; i < 60; i++)
            rewards.Add(new List<RewardItem>());
        // month 1
        rewards[0].Add(new RewardItem(169630007, 1)); // [Expand Card] Expand Cube Ticket (lvl 4)
        rewards[0].Add(new RewardItem(169620094, 1)); // Crafting Boost Charm III - 100%
        rewards[0].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[0].Add(new RewardItem(162002030, 50)); // [Event] Premium Restoration Serum

        // month 2
        rewards[1].Add(new RewardItem(190020075, 1)); // Flash Bogel Egg
        rewards[1].Add(new RewardItem(169600064, 1)); // [Emotion Card] Playing Dead
        rewards[1].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[1].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 3
        rewards[2].Add(new RewardItem(125040038, 1)); // Devil Horns
        rewards[2].Add(new RewardItem(186000199, 100)); // Legion Coin
        rewards[2].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[2].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%

        // month 4
        rewards[3].Add(new RewardItem(169640006, 1)); // [Expand Card] Expand Warehouse Ticket (lvl 4)
        rewards[3].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[3].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[3].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 5
        rewards[4].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[4].Add(new RewardItem(169600103, 1)); // [Emotion Card] Diving
        rewards[4].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[4].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[4].Add(new RewardItem(186000051, 5)); // Major Ancient Crown

        // month 6
        rewards[5].Add(new RewardItem(187000057, 1)); // Kahrun's Wing
        rewards[5].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[5].Add(new RewardItem(164002264, 25)); // Flame Pillar Firecracker
        rewards[5].Add(new RewardItem(169670000, 1)); // Name Change Ticket

        // month 7
        rewards[6].Add(new RewardItem(169630007, 1)); // [Expand Card] Expand Cube Ticket (lvl 4)
        rewards[6].Add(new RewardItem(169600065, 1)); // [Emotion Card] Sing
        rewards[6].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%
        rewards[6].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[6].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 8
        rewards[7].Add(new RewardItem(190000048, 1)); // Golden Nyanco Egg
        rewards[7].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[7].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[7].Add(new RewardItem(186000199, 100)); // Legion Coin

        // month 9
        rewards[8].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[8].Add(new RewardItem(169600087, 1)); // [Emotion Card] 'Bad Girl' Dance
        rewards[8].Add(new RewardItem(188052761, 5)); // [Event] Bonus Entry Scroll Bundle
        rewards[8].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 10
        rewards[9].Add(new RewardItem(169640006, 1)); // [Expand Card] Expand Warehouse Ticket (lvl 4)
        rewards[9].Add(new RewardItem(169650007, 1)); // [Event] Plastic Surgery Ticket
        rewards[9].Add(new RewardItem(186000051, 5)); // Major Ancient Crown
        rewards[9].Add(new RewardItem(161001001, 5)); // Revival Stone

        // month 11
        rewards[10].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[10].Add(new RewardItem(164002284, 25)); // [Event] Ornate Firecrackers
        rewards[10].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[10].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%

        // month 12
        rewards[11].Add(new RewardItem(190100107, 1)); // Emerald Crestlich
        rewards[11].Add(new RewardItem(169600062, 1)); // [Emotion Card] Play Harp
        rewards[11].Add(new RewardItem(169610343, 1)); // [Title] Forgotten Hero
        rewards[11].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone

        // month 13
        rewards[12].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[12].Add(new RewardItem(169660003, 1)); // [Event] Gender Switch Ticket
        rewards[12].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[12].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 14
        rewards[13].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[13].Add(new RewardItem(169600063, 1)); // [Emotion Card] Play the Saxophone
        rewards[13].Add(new RewardItem(188052761, 5)); // [Event] Bonus Entry Scroll Bundle
        rewards[13].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 15
        rewards[14].Add(new RewardItem(110900876, 1)); // Nyerkcarrier
        rewards[14].Add(new RewardItem(190020156, 1)); // [Event] Medalist Shugo Egg
        rewards[14].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[14].Add(new RewardItem(161001001, 5)); // Revival Stone

        // month 16
        rewards[15].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[15].Add(new RewardItem(169600060, 1)); // [Emotion Card] Play the Drum
        rewards[15].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[15].Add(new RewardItem(186000051, 5)); // Major Ancient Crown
        rewards[15].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%

        // month 17
        rewards[16].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[16].Add(new RewardItem(164002284, 25)); // [Event] Ornate Firecrackers
        rewards[16].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[16].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[16].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 18
        rewards[17].Add(new RewardItem(187060162, 1)); // Wings of Agony
        rewards[17].Add(new RewardItem(168310018, 1)); // Major Blessed Augment: Level 2
        rewards[17].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[17].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 19
        rewards[18].Add(new RewardItem(125050026, 1)); // Elcoro Hat
        rewards[18].Add(new RewardItem(186000077, 1)); // Hot Heart of Magic
        rewards[18].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[18].Add(new RewardItem(161001001, 5)); // Revival Stone

        // month 20
        rewards[19].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[19].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[19].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[19].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[19].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 21
        rewards[20].Add(new RewardItem(169630007, 1)); // [Expand Card] Expand Cube Ticket (lvl 4)
        rewards[20].Add(new RewardItem(169600039, 1)); // [Emotion Card] Chew Bubblegum
        rewards[20].Add(new RewardItem(186000238, 150)); // Conqueror's Herb
        rewards[20].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 22
        rewards[21].Add(new RewardItem(169640006, 1)); // [Expand Card] Expand Warehouse Ticket (lvl 4)
        rewards[21].Add(new RewardItem(152012593, 3)); // Valor's Heart
        rewards[21].Add(new RewardItem(152012587, 3)); // Wind Eternity
        rewards[21].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[21].Add(new RewardItem(164002284, 25)); // [Event] Ornate Firecrackers

        // month 23
        rewards[22].Add(new RewardItem(188508017, 1)); // [Motion Card] Stormbringer
        rewards[22].Add(new RewardItem(188053609, 3)); // [Event] Level 60 Composite Manastone Bundle
        rewards[22].Add(new RewardItem(166200009, 3)); // Mythic Weapon Tuning Scroll
        rewards[22].Add(new RewardItem(166200010, 3)); // Mythic Armor Tuning Scroll

        // month 24
        rewards[23].Add(new RewardItem(169650007, 1)); // [Event] Plastic Surgery Ticket
        rewards[23].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[23].Add(new RewardItem(152012586, 2)); // Wind Breath
        rewards[23].Add(new RewardItem(152012581, 2)); // Fire Breath
        rewards[23].Add(new RewardItem(186000238, 150)); // Conqueror's Mark

        // month 25
        rewards[24].Add(new RewardItem(169630007, 1)); // [Expand Card] Expand Cube Ticket (lvl 4)
        rewards[24].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[24].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%
        rewards[24].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 26
        rewards[25].Add(new RewardItem(169640006, 1)); // [Expand Card] Expand Warehouse Ticket (lvl 4)
        rewards[25].Add(new RewardItem(152012593, 3)); // Valor's Heart
        rewards[25].Add(new RewardItem(152012590, 3)); // Wind Origin
        rewards[25].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[25].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone

        // month 27
        rewards[26].Add(new RewardItem(188500014, 1)); // [Motion Card] The Dragon's Set
        rewards[26].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[26].Add(new RewardItem(164002116, 25)); // [Event] Rx: Accelerox
        rewards[26].Add(new RewardItem(164002117, 25)); // [Event] Rx: Blitzopan
        rewards[26].Add(new RewardItem(164002118, 25)); // [Event] Rx: Castafodin

        // month 28
        rewards[27].Add(new RewardItem(110900731, 1)); // Cogwheel Couture
        rewards[27].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[27].Add(new RewardItem(152012586, 2)); // Wind Breath
        rewards[27].Add(new RewardItem(152012581, 2)); // Fire Breath

        // month 29
        rewards[28].Add(new RewardItem(169600186, 1)); // [Emotion Card] Sing "Good Day"
        rewards[28].Add(new RewardItem(166200009, 3)); // Mythic Weapon Tuning Scroll
        rewards[28].Add(new RewardItem(166200010, 3)); // Mythic Armor Tuning Scroll
        rewards[28].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 30
        rewards[29].Add(new RewardItem(169610137, 1)); // [Title Card] Aion's Chosen
        rewards[29].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[29].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[29].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 31
        rewards[30].Add(new RewardItem(166030007, 3)); // [Event] Tempering Solution
        rewards[30].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[30].Add(new RewardItem(164002116, 25)); // [Event] Rx: Accelerox
        rewards[30].Add(new RewardItem(164002117, 25)); // [Event] Rx: Blitzopan
        rewards[30].Add(new RewardItem(164002118, 25)); // [Event] Rx: Castafodin

        // month 32
        rewards[31].Add(new RewardItem(169600086, 1)); // [Emotion Card] 'Shut Up' Dance
        rewards[31].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[31].Add(new RewardItem(188053609, 3)); // [Event] Level 60 Composite Manastone Bundle
        rewards[31].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[31].Add(new RewardItem(188052761, 5)); // [Event] Bonus Entry Scroll Bundle

        // month 33
        rewards[32].Add(new RewardItem(187060178, 1)); // Aether Glider
        rewards[32].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[32].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish
        rewards[32].Add(new RewardItem(161001001, 5)); // Revival Stone

        // month 34
        rewards[33].Add(new RewardItem(168310018, 1)); // Major Blessed Augment: Level 2
        rewards[33].Add(new RewardItem(188052638, 1)); // [Event] Fabled Godstone Bundle
        rewards[33].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[33].Add(new RewardItem(164002284, 25)); // [Event] Ornate Firecrackers

        // month 35
        rewards[34].Add(new RewardItem(169600102, 1)); // [Emotion Card] Floor Sweep
        rewards[34].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[34].Add(new RewardItem(164002272, 25)); // [Event] Enduring Greater Raging Wind Scroll
        rewards[34].Add(new RewardItem(162000141, 25)); // Sublime Wind Serum
        rewards[34].Add(new RewardItem(186000238, 150)); // Conqueror's Mark

        // month 36
        rewards[35].Add(new RewardItem(190100042, 1)); // Legion Pagati
        rewards[35].Add(new RewardItem(166030007, 3)); // [Event] Tempering Solution
        rewards[35].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[35].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%

        // month 37
        rewards[36].Add(new RewardItem(169650007, 1)); // [Event] Plastic Surgery Ticket
        rewards[36].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[36].Add(new RewardItem(164002116, 25)); // [Event] Rx: Accelerox
        rewards[36].Add(new RewardItem(164002117, 25)); // [Event] Rx: Blitzopan
        rewards[36].Add(new RewardItem(164002118, 25)); // [Event] Rx: Castafodin

        // month 38
        rewards[37].Add(new RewardItem(165020016, 1)); // Accessory Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[37].Add(new RewardItem(188053610, 3)); // [Event] Level 70 Composite Manastone Bundle
        rewards[37].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[37].Add(new RewardItem(186000399, 100)); // Honorable Conqueror's Mark

        // month 39
        rewards[38].Add(new RewardItem(110900695, 1)); // Biker Costume
        rewards[38].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[38].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%
        rewards[38].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish

        // month 40
        rewards[39].Add(new RewardItem(125045415, 1)); // Biker Hat
        rewards[39].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[39].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[39].Add(new RewardItem(186000199, 150)); // Legion Coin

        // month 41
        rewards[40].Add(new RewardItem(165020015, 1)); // Armor Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[40].Add(new RewardItem(152012593, 3)); // Valor's Heart
        rewards[40].Add(new RewardItem(152012587, 3)); // Wind Eternity
        rewards[40].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone

        // month 42
        rewards[41].Add(new RewardItem(168310018, 1)); // Major Blessed Augment: Level 2
        rewards[41].Add(new RewardItem(186000051, 5)); // Major Ancient Crown
        rewards[41].Add(new RewardItem(166200009, 3)); // Mythic Weapon Tuning Scroll
        rewards[41].Add(new RewardItem(166200010, 3)); // Mythic Armor Tuning Scroll

        // month 43
        rewards[42].Add(new RewardItem(188508005, 1)); // [Motion Card] Socialite
        rewards[42].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[42].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%
        rewards[42].Add(new RewardItem(152012590, 3)); // Wind Origin
        rewards[42].Add(new RewardItem(186000238, 150)); // Conqueror's Mark

        // month 44
        rewards[43].Add(new RewardItem(165020014, 1)); // Weapon Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[43].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[43].Add(new RewardItem(188053610, 3)); // [Event] Level 70 Composite Manastone Bundle
        rewards[43].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[43].Add(new RewardItem(188052761, 5)); // [Event] Bonus Entry Scroll Bundle

        // month 45
        rewards[44].Add(new RewardItem(169600217, 1)); // [Emotion Card] Summer Vacation
        rewards[44].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[44].Add(new RewardItem(164002272, 25)); // [Event] Enduring Greater Raging Wind Scroll
        rewards[44].Add(new RewardItem(162000141, 25)); // Sublime Wind Serum

        // month 46
        rewards[45].Add(new RewardItem(170100041, 1)); // Club Speaker Cabinet
        rewards[45].Add(new RewardItem(152012586, 2)); // Wind Breath
        rewards[45].Add(new RewardItem(152012581, 2)); // Fire Breath
        rewards[45].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone

        // month 47
        rewards[46].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[46].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[46].Add(new RewardItem(186000242, 5)); // Ceramium Medal
        rewards[46].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[46].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 48
        rewards[47].Add(new RewardItem(169610158, 1)); // [Title Card] Prestigious Adept
        rewards[47].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish
        rewards[47].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[47].Add(new RewardItem(186000399, 100)); // Honorable Conqueror's Mark

        // month 49
        rewards[48].Add(new RewardItem(169600098, 1)); // [Emotion Card] Hug Me
        rewards[48].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[48].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        rewards[48].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[48].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%

        // month 50
        rewards[49].Add(new RewardItem(165020015, 1)); // Armor Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[49].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[49].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[49].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum

        // month 51
        rewards[50].Add(new RewardItem(110900603, 1)); // Lawful Uniform
        rewards[50].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[50].Add(new RewardItem(164002116, 25)); // [Event] Rx: Accelerox
        rewards[50].Add(new RewardItem(164002117, 25)); // [Event] Rx: Blitzopan
        rewards[50].Add(new RewardItem(164002118, 25)); // [Event] Rx: Castafodin

        // month 52
        rewards[51].Add(new RewardItem(125045283, 1)); // Lawful Headgear
        rewards[51].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[51].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[51].Add(new RewardItem(164002284, 15)); // [Event] Ornate Firecrackers
        rewards[51].Add(new RewardItem(186000409, 150)); // Daeva's Respite Coin

        // month 53
        rewards[52].Add(new RewardItem(165020016, 1)); // Accessory Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[52].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[52].Add(new RewardItem(186000051, 5)); // Major Ancient Crown
        rewards[52].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle

        // month 54
        rewards[53].Add(new RewardItem(169670000, 1)); // Name Change Ticket
        rewards[53].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish
        rewards[53].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[53].Add(new RewardItem(164002272, 25)); // [Event] Enduring Greater Raging Wind Scroll
        rewards[53].Add(new RewardItem(162000141, 25)); // Sublime Wind Serum

        // month 55
        rewards[54].Add(new RewardItem(168310018, 1)); // Major Blessed Augment: Level 2
        rewards[54].Add(new RewardItem(162000137, 25)); // Sublime Life Serum
        rewards[54].Add(new RewardItem(162000139, 25)); // Sublime Mana Serum
        rewards[54].Add(new RewardItem(188053610, 3)); // [Event] Level 70 Composite Manastone Bundle
        rewards[54].Add(new RewardItem(164002284, 15)); // [Event] Ornate Firecrackers

        // month 56
        rewards[55].Add(new RewardItem(165020014, 1)); // Weapon Wrapping Scroll (Eternal/Lv. 65 and lower)
        rewards[55].Add(new RewardItem(186000242, 15)); // Ceramium Medal
        rewards[55].Add(new RewardItem(169620072, 3)); // AP Boost Charm II - 30%
        rewards[55].Add(new RewardItem(188053618, 1)); // Honorable Elim's Idian Bundle

        // month 57
        rewards[56].Add(new RewardItem(166200009, 3)); // Mythic Weapon Tuning Scroll
        rewards[56].Add(new RewardItem(161001001, 5)); // Revival Stone
        rewards[56].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[56].Add(new RewardItem(166100023, 1000)); // [Stamp] High Grade Enchanting Supplement (Mythic)

        // month 58
        rewards[57].Add(new RewardItem(190010001, 1)); // Potbelly Inquin Egg
        rewards[57].Add(new RewardItem(188052761, 5)); // [Event] Bonus Entry Scroll Bundle
        rewards[57].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        rewards[57].Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        rewards[57].Add(new RewardItem(166150026, 2)); // [Stamp] Greater Felicitous Socketing (Heroic)

        // month 59
        rewards[58].Add(new RewardItem(166200010, 3)); // Mythic Armor Tuning Scroll
        rewards[58].Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        rewards[58].Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        rewards[58].Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution

        // month 60
        rewards[59].Add(new RewardItem(188053996, 1)); // Emperor Trillirunerk's Feather Box
        rewards[59].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish
        rewards[59].Add(new RewardItem(186000399, 125)); // Honorable Conqueror's Mark
        rewards[59].Add(new RewardItem(166150027, 2)); // [Stamp] Greater Felicitous Socketing (Mythic)

        // random rewards for month 61+
        randomRewards.Add(new RewardItem(161001001, 5)); // Revival Stone
        randomRewards.Add(new RewardItem(162000137, 15)); // Sublime Life Serum
        randomRewards.Add(new RewardItem(162000139, 15)); // Sublime Mana Serum
        randomRewards.Add(new RewardItem(162000141, 15)); // Sublime Wind Serum
        randomRewards.Add(new RewardItem(164002167, 15)); // Drana Coffee
        randomRewards.Add(new RewardItem(188054198, 3)); // Greater Scroll Bundle
        randomRewards.Add(new RewardItem(186000051, 5)); // Major Ancient Crown
        randomRewards.Add(new RewardItem(186000247, 5)); // Major Danuar Relic
        randomRewards.Add(new RewardItem(188053666, 2)); // [Event] Ceramium Medal Box
        randomRewards.Add(new RewardItem(188053667, 1)); // [Event] Mithril Medal Box
        randomRewards.Add(new RewardItem(186000243, 10)); // Fragmented Ceramium
        randomRewards.Add(new RewardItem(186000236, 75)); // Blood Mark
        randomRewards.Add(new RewardItem(188053610, 3)); // [Event] Level 70 Composite Manastone Bundle
        randomRewards.Add(new RewardItem(169620094, 1)); // Crafting Boost Charm III - 100%
        randomRewards.Add(new RewardItem(169620082, 1)); // Gathering Boost Charm II - 100%
        randomRewards.Add(new RewardItem(169620072, 1)); // AP Boost Charm II - 30%
        randomRewards.Add(new RewardItem(166020003, 5)); // [Event] Omega Enchantment Stone
        randomRewards.Add(new RewardItem(166030007, 5)); // [Event] Tempering Solution
        randomRewards.Add(new RewardItem(166500005, 5)); // [Event] Amplification Stone
        randomRewards.Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        randomRewards.Add(new RewardItem(188052719, 5)); // [Event] Dye Bundle
        randomRewards.Add(new RewardItem(186000238, 150)); // Conqueror's Herb
        randomRewards.Add(new RewardItem(186000399, 125)); // Honorable Conqueror's Mark
        randomRewards.Add(new RewardItem(186000409, 50)); // Daeva's Respite Coin
        randomRewards.Add(new RewardItem(188052761, 3)); // [Event] Bonus Entry Scroll Bundle
        randomRewards.Add(new RewardItem(166150018, 3)); // Assured Greater Felicitous Socketing (Eternal)
        randomRewards.Add(new RewardItem(166150019, 3)); // Assured Greater Felicitous Socketing (Mythic)
        randomRewards.Add(new RewardItem(166100020, 250)); // [Stamp] High Grade Enchanting Supplement (Eternal)
        randomRewards.Add(new RewardItem(166100023, 250)); // [Stamp] High Grade Enchanting Supplement (Mythic)
    }

    /// <summary>Prevent instantiation</summary>
    private VeteranRewardService()
    {
    }

    public static VeteranRewardService GetInstance()
    {
        return SingletonHolder.instance;
    }

    public void TryReward(Player player)
    {
        if (player.GetLevel() != 65)
            return;

        DateTimeOffset now = ServerTime.Now();
        DateTimeOffset charCreationTime = ServerTime.AtDate(player.GetCreationDate());
        if (MonthsBetween(charCreationTime, now) < 1) // return if char is younger than a month
            return;

        DateTimeOffset accCreationTime = ServerTime.OfEpochMilli(player.GetAccount().GetCreationDate());
        int maxMonthsToReceive = (int)MonthsBetween(accCreationTime, now);
        if (maxMonthsToReceive < 1) // return if account is younger than a month
            return;

        int receivedMonths = VeteranRewardDAO.LoadReceivedMonths(player); // -1 means error
        if (receivedMonths < 0 || receivedMonths >= maxMonthsToReceive)
            return;

        if (VeteranRewardDAO.StoreReceivedMonths(player, maxMonthsToReceive))
            for (int i = receivedMonths; i < maxMonthsToReceive; i++)
            {
                List<RewardItem> items;
                if (i < 60)
                {
                    items = rewards[i];
                }
                else
                {
                    items = new List<RewardItem>(randomRewards);
                    while (items.Count > RANDOM_ITEMS_PER_MONTH)
                        items.RemoveAt(Rnd.NextInt(items.Count));
                }
                if (player.GetMailbox().GetLetters().Count >= 100)
                { // abort on mailbox overflow and save the correct month
                    VeteranRewardDAO.StoreReceivedMonths(player, i);
                    return;
                }
                foreach (RewardItem item in items)
                    SystemMailService.SendMail("Beyond Aion", player.GetName(), "Veteran Reward",
                        "Greetings Daeva!\n\nIt has been over " + (i == 0 ? "a month" : (i + 1) + " months")
                            + " now, since you joined us.\nWe send you this and hope you stay with us even longer :)\n\n~ Beyond Aion",
                        item.GetId(), item.GetCount(), 0, LetterType.BLACKCLOUD);
            }
    }

    /// <summary>Java parity: ChronoUnit.MONTHS.between(start, end) — count of complete months (may be negative).</summary>
    private static long MonthsBetween(DateTimeOffset start, DateTimeOffset end)
    {
        long months = (end.Year - start.Year) * 12L + (end.Month - start.Month);
        if (months > 0 && start.AddMonths((int)months) > end)
            months--;
        else if (months < 0 && start.AddMonths((int)months) < end)
            months++;
        return months;
    }

    private static class SingletonHolder
    {
        internal static readonly VeteranRewardService instance = new VeteranRewardService();
    }
}
