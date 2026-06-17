using System.Collections.Generic;
using Aion.Commons.Network;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Emotion;
using Aion.GameServer.Model.GameObjects.Players.Motion;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.GameObjects.Players.Title;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Transfers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_PTRANSFER_CONTROL (KID) — cross-server character transfer control.
/// </summary>
public sealed class SM_PTRANSFER_CONTROL : LoginServerPacket
{
    private static readonly ILogger Log = NullLogger.Instance;

    public const byte CHARACTER_INFORMATION = 1;
    public const byte ITEMS_INFORMATION = 5;
    public const byte DATA_INFORMATION = 6;
    public const byte SKILL_INFORMATION = 7;
    public const byte RECIPE_INFORMATION = 8;
    public const byte QUEST_INFORMATION = 9;
    public const byte ERROR = 2;
    public const byte OK = 3;
    public const byte TASK_STOP = 4;

    private readonly byte _type;
    private readonly int _taskId;
    private readonly string? _result;
    private readonly Player? _player;

    public SM_PTRANSFER_CONTROL(byte type, int taskId)
    {
        _type = type;
        _taskId = taskId;
    }

    public SM_PTRANSFER_CONTROL(byte type, int taskId, string result)
    {
        _type = type;
        _taskId = taskId;
        _result = result;
    }

    public SM_PTRANSFER_CONTROL(byte type, TransferablePlayer tp)
    {
        _type = type;
        _taskId = tp.taskId;
        _player = tp.player;
    }

    // Java parity (writeImpl): 1:1 with game-server/.../loginserver/serverpackets/SM_PTRANSFER_CONTROL.java.
    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(_type);
        switch (_type)
        {
            case OK:
                buffer.WriteD(_taskId);
                break;
            case ERROR:
                buffer.WriteD(_taskId);
                buffer.WriteS(_result);
                break;
            case TASK_STOP:
                buffer.WriteD(_taskId);
                buffer.WriteS(_result);
                break;
            case CHARACTER_INFORMATION:
                buffer.WriteD(_taskId);
                buffer.WriteS(_player!.GetName(false));
                buffer.WriteD(_player.GetPlayerClass().GetClassId());
                buffer.WriteQ(_player.GetCommonData().GetExp());
                buffer.WriteD(_player.GetRace().GetRaceId());
                buffer.WriteD(_player.GetCommonData().GetGender().GetGenderId());
                buffer.WriteD(_player.GetCommonData().GetTitleId());
                buffer.WriteD(_player.GetCommonData().GetDp());
                buffer.WriteD(_player.GetCommonData().GetQuestExpands());
                buffer.WriteD(_player.GetCommonData().GetNpcExpands());
                buffer.WriteD(_player.GetCommonData().GetItemExpands());
                buffer.WriteD(_player.GetCommonData().GetWhNpcExpands());

                PlayerAppearance playerAppearance = _player.GetPlayerAppearance();
                buffer.WriteD(playerAppearance.GetSkinRGB());
                buffer.WriteD(playerAppearance.GetHairRGB());
                buffer.WriteD(playerAppearance.GetEyeRGB());
                buffer.WriteD(playerAppearance.GetLipRGB());
                buffer.WriteC(playerAppearance.GetFace());
                buffer.WriteC(playerAppearance.GetHair());
                buffer.WriteC(playerAppearance.GetDeco());
                buffer.WriteC(playerAppearance.GetTattoo());
                buffer.WriteC(playerAppearance.GetFaceContour());
                buffer.WriteC(playerAppearance.GetExpression());
                buffer.WriteC(playerAppearance.GetJawLine());
                buffer.WriteC(playerAppearance.GetForehead());
                buffer.WriteC(playerAppearance.GetEyeHeight());
                buffer.WriteC(playerAppearance.GetEyeSpace());
                buffer.WriteC(playerAppearance.GetEyeWidth());
                buffer.WriteC(playerAppearance.GetEyeSize());
                buffer.WriteC(playerAppearance.GetEyeShape());
                buffer.WriteC(playerAppearance.GetEyeAngle());
                buffer.WriteC(playerAppearance.GetBrowHeight());
                buffer.WriteC(playerAppearance.GetBrowAngle());
                buffer.WriteC(playerAppearance.GetBrowShape());
                buffer.WriteC(playerAppearance.GetNose());
                buffer.WriteC(playerAppearance.GetNoseBridge());
                buffer.WriteC(playerAppearance.GetNoseWidth());
                buffer.WriteC(playerAppearance.GetNoseTip());
                buffer.WriteC(playerAppearance.GetCheek());
                buffer.WriteC(playerAppearance.GetLipHeight());
                buffer.WriteC(playerAppearance.GetMouthSize());
                buffer.WriteC(playerAppearance.GetLipSize());
                buffer.WriteC(playerAppearance.GetSmile());
                buffer.WriteC(playerAppearance.GetLipShape());
                buffer.WriteC(playerAppearance.GetJawHeigh());
                buffer.WriteC(playerAppearance.GetChinJut());
                buffer.WriteC(playerAppearance.GetEarShape());
                buffer.WriteC(playerAppearance.GetHeadSize());
                buffer.WriteC(playerAppearance.GetNeck());
                buffer.WriteC(playerAppearance.GetNeckLength());
                buffer.WriteC(playerAppearance.GetShoulderSize());
                buffer.WriteC(playerAppearance.GetTorso());
                buffer.WriteC(playerAppearance.GetChest()); // only woman
                buffer.WriteC(playerAppearance.GetWaist());
                buffer.WriteC(playerAppearance.GetHips());
                buffer.WriteC(playerAppearance.GetArmThickness());
                buffer.WriteC(playerAppearance.GetHandSize());
                buffer.WriteC(playerAppearance.GetLegThickness());
                buffer.WriteC(playerAppearance.GetFootSize());
                buffer.WriteC(playerAppearance.GetFacialRate());
                buffer.WriteC(playerAppearance.GetArmLength());
                buffer.WriteC(playerAppearance.GetLegLength());
                buffer.WriteC(playerAppearance.GetShoulders());
                buffer.WriteC(playerAppearance.GetFaceShape());
                buffer.WriteC(playerAppearance.GetVoice());
                buffer.WriteF(playerAppearance.GetHeight());

                buffer.WriteF(_player.GetX());
                buffer.WriteF(_player.GetY());
                buffer.WriteF(_player.GetZ());
                buffer.WriteC(_player.GetHeading());
                buffer.WriteD(_player.GetWorldId());
                break;
            case ITEMS_INFORMATION:
                buffer.WriteD(_taskId);
                // inventory
                List<Item> inv = InventoryDAO.LoadItems(_player!.GetObjectId(), StorageType.CUBE);
                inv.AddRange(InventoryDAO.LoadItems(_player.GetObjectId(), StorageType.REGULAR_WAREHOUSE));
                ItemStoneListDAO.Load(inv);
                buffer.WriteD(inv.Count);
                foreach (Item item in inv)
                {
                    buffer.WriteD(item.GetObjectId());
                    buffer.WriteD(item.GetItemId());
                    buffer.WriteQ(item.GetItemCount());
                    buffer.WriteD(item.GetItemColor() == null ? -1 : item.GetItemColor()!.Value);

                    buffer.WriteS(item.GetItemCreator());
                    buffer.WriteD(item.GetExpireTime());
                    buffer.WriteD(item.GetActivationCount());
                    buffer.WriteC(item.IsEquipped() ? 1 : 0);

                    buffer.WriteC(item.IsSoulBound() ? 1 : 0);
                    buffer.WriteQ(item.GetEquipmentSlot());
                    buffer.WriteD(item.GetItemLocation());
                    buffer.WriteD(item.GetEnchantLevel());
                    buffer.WriteD(item.GetEnchantBonus());

                    buffer.WriteD(item.GetItemSkinTemplate().GetTemplateId());
                    buffer.WriteD(item.GetFusionedItemId());
                    buffer.WriteD(item.GetOptionalSockets());
                    buffer.WriteD(item.GetFusionedItemOptionalSockets());

                    buffer.WriteD(item.GetChargePoints());
                    ISet<ManaStone> itemStones = item.GetItemStones();
                    buffer.WriteC(itemStones.Count);
                    foreach (ManaStone stone in itemStones)
                    {
                        buffer.WriteD(stone.GetItemId());
                        buffer.WriteD(stone.GetSlot());
                    }
                    itemStones = item.GetFusionStones();
                    buffer.WriteC(itemStones.Count);
                    foreach (ManaStone stone in itemStones)
                    {
                        buffer.WriteD(stone.GetItemId());
                        buffer.WriteD(stone.GetSlot());
                    }
                    buffer.WriteD(item.GetGodStoneId());
                    buffer.WriteD(item.GetColorExpireTime());
                    buffer.WriteD(item.GetTuneCount());
                    buffer.WriteD(item.GetBonusStatsId());
                    buffer.WriteD(item.GetFusionedItemBonusStatsId());
                    buffer.WriteD(item.GetTempering());
                    buffer.WriteD(item.GetPackCount());
                    buffer.WriteC(item.IsAmplified() ? 1 : 0);
                    buffer.WriteH(item.GetBuffSkill());
                }
                break;
            case DATA_INFORMATION:
                buffer.WriteD(_taskId);
                EmotionList emo = _player!.GetEmotions();
                buffer.WriteD(emo.GetEmotions().Count);
                foreach (Emotion e in emo.GetEmotions())
                {
                    buffer.WriteD(e.GetId());
                    buffer.WriteD(((IExpirable)e).SecondsUntilExpiration());
                }

                MotionList motions = _player.GetMotions();
                buffer.WriteD(motions.GetMotions().Count);
                foreach (Motion motion in motions.GetMotions().Values)
                {
                    buffer.WriteD(motion.GetId());
                    buffer.WriteD(motion.GetExpireTime());
                    buffer.WriteC(motion.IsActive() ? 1 : 0);
                }

                List<Macros.Macro> macros = _player.GetMacros().GetAll();
                buffer.WriteD(macros.Count);
                foreach (Macros.Macro m in macros)
                {
                    buffer.WriteD(m.Id);
                    buffer.WriteS(m.Xml);
                }

                NpcFactions nf = _player.GetNpcFactions();
                buffer.WriteD(nf.GetNpcFactions().Count);
                foreach (NpcFaction f in nf.GetNpcFactions())
                {
                    buffer.WriteD(f.GetId());
                    buffer.WriteD(f.GetTime());
                    buffer.WriteC(f.IsActive() ? 1 : 0);
                    buffer.WriteS(f.GetState().ToString());
                    buffer.WriteD(f.GetQuestId());
                }

                ICollection<PetCommonData> pets = _player.GetPetList().GetPets();
                buffer.WriteD(pets.Count);
                foreach (PetCommonData pet in pets)
                {
                    buffer.WriteD(pet.GetTemplateId());
                    buffer.WriteD(pet.GetDecoration());
                    buffer.WriteQ(pet.GetBirthdayTimestamp() == null ? 0
                        : new System.DateTimeOffset(pet.GetBirthdayTimestamp()!.Value.ToUniversalTime()).ToUnixTimeMilliseconds());
                    buffer.WriteS(pet.GetName());
                    buffer.WriteD(pet.GetExpireTime()); // 26-08-2013 kid
                }
                TitleList titles = _player.GetTitleList();
                buffer.WriteD(titles.GetTitles().Count);
                foreach (Title t in titles.GetTitles())
                {
                    buffer.WriteD(t.GetId());
                    buffer.WriteD(((IExpirable)t).SecondsUntilExpiration());
                }

                global::Aion.GameServer.Model.GameObjects.Players.PlayerSettings ps = _player.GetPlayerSettings();
                buffer.WriteD(ps.GetUiSettings() == null ? 0 : ps.GetUiSettings()!.Length);
                buffer.WriteD(ps.GetShortcuts() == null ? 0 : ps.GetShortcuts()!.Length);
                if (ps.GetUiSettings() != null)
                {
                    buffer.WriteB(ps.GetUiSettings()!);
                }
                if (ps.GetShortcuts() != null)
                {
                    buffer.WriteB(ps.GetShortcuts()!);
                }
                buffer.WriteD(ps.GetDeny());
                buffer.WriteD(ps.GetDisplay());
                break;
            case SKILL_INFORMATION:
                buffer.WriteD(_taskId);
                PlayerSkillList skillList = _player!.GetSkillList();

                // discard stigma skills
                List<PlayerSkillEntry> skills = new List<PlayerSkillEntry>();
                foreach (PlayerSkillEntry sk in skillList.GetAllSkills())
                {
                    if (!sk.IsStigmaSkill())
                    {
                        skills.Add(sk);
                    }
                }

                buffer.WriteD(skills.Count);
                foreach (PlayerSkillEntry sk in skills)
                {
                    buffer.WriteD(sk.GetSkillId());
                    buffer.WriteD(sk.GetSkillLevel());
                }

                break;
            case RECIPE_INFORMATION:
                buffer.WriteD(_taskId);
                RecipeList rec = _player!.GetRecipeList();
                buffer.WriteD(rec.GetRecipeList().Count);
                foreach (int id in rec.GetRecipeList())
                {
                    buffer.WriteD(id);
                }
                break;
            case QUEST_INFORMATION:
                buffer.WriteD(_taskId);
                QuestStateList qsl = _player!.GetQuestStateList();
                List<QuestState> quests = new List<QuestState>();
                foreach (QuestState qs in qsl.GetAllQuestState())
                {
                    if (qs == null)
                    {
                        Log.LogWarning("there are null quest on player {Name}. taskId #{TaskId}. transfer skip that", _player.GetName(false), _taskId);
                        continue;
                    }
                    quests.Add(qs);
                }
                buffer.WriteD(quests.Count);
                foreach (QuestState qs in quests)
                {
                    buffer.WriteD(qs.GetQuestId());
                    buffer.WriteS(qs.GetStatus().ToString());
                    buffer.WriteD(qs.GetQuestVars().GetQuestVars());
                    buffer.WriteD(qs.GetCompleteCount());
                    buffer.WriteD(qs.GetRewardGroup() == null ? -1 : qs.GetRewardGroup()!.Value);
                    buffer.WriteQ(new System.DateTimeOffset(qs.GetLastCompleteTime()!.Value.ToUniversalTime()).ToUnixTimeMilliseconds());
                    buffer.WriteQ(new System.DateTimeOffset(qs.GetNextRepeatTime()!.Value.ToUniversalTime()).ToUnixTimeMilliseconds());
                    buffer.WriteD(qs.GetFlags());
                }
                break;
        }
    }
}
