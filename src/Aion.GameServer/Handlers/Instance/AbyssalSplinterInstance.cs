using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/AbyssalSplinterInstance (zhkchi, vlog, Luzien) : GeneralInstanceHandler. @InstanceID(300220000). 1:1.</summary>
[InstanceID(300220000)]
public class AbyssalSplinterInstance : GeneralInstanceHandler
{
    private int destroyedFragments;
    private int killedPazuzuWorms;

    public AbyssalSplinterInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnSpawn(VisibleObject @object)
    {
        if (@object is Npc npc)
        {
            switch (npc.GetNpcId())
            {
                case 216960: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdDH_Wakeup()); break;
                case 216952: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_Wakeup()); break;
            }
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        int npcId = npc.GetNpcId();
        switch (npcId)
        {
            case 216951: // Pazuzu
                SpawnPazuzuFragment();
                SpawnPazuzuTreasureBoxes();
                break;
            case 216950: // Kaluva the Fourth Fragment
                SpawnKaluvaFragment();
                SpawnKaluvaTreasureBoxes();
                break;
            case 216948: // Rukril
            case 216949: // Ebonsoul
                if (GetNpc(npcId == 216949 ? 216948 : 216949) == null)
                {
                    SpawnDayshadeFragment();
                    SpawnDayshadeTreasureBoxes();
                }
                else
                {
                    SendMsg(npcId == 216948 ? SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_Light_Die() : SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_Dark_Die());
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (GetNpc(npcId == 216949 ? 216948 : 216949) != null)
                        {
                            switch (npcId)
                            {
                                case 216948: Spawn(216948, 447.1937f, 683.72217f, 433.1805f, (byte)108); break; // rukril
                                case 216949: Spawn(216949, 455.5502f, 702.09485f, 433.13727f, (byte)108); break; // ebonsoul
                            }
                        }
                        return ValueTask.CompletedTask;
                    }, 60000);
                }
                npc.GetController().Delete();
                break;
            case 281907: // Piece of Splendor
                Npc ebonsoul = GetNpc(216949);
                if (ebonsoul != null && !ebonsoul.IsDead())
                {
                    if (PositionUtil.IsInRange(npc, ebonsoul, 5))
                    {
                        ebonsoul.GetEffectController().RemoveEffect(19159);
                        DeleteAliveNpcs(281907);
                        break;
                    }
                }
                npc.GetController().Delete();
                break;
            case 281908: // Piece of Midnight
                Npc rukril = GetNpc(216948);
                if (rukril != null && !rukril.IsDead())
                {
                    if (PositionUtil.IsInRange(npc, rukril, 5))
                    {
                        rukril.GetEffectController().RemoveEffect(19266);
                        DeleteAliveNpcs(281908);
                        break;
                    }
                }
                npc.GetController().Delete();
                break;
            case 216960: // Yamennes Painflare
            case 216952: // Yamennes Blindsight
                SpawnYamennesTreasureBoxes(npcId == 216952 ? 700937 : 700938);
                DeleteAliveNpcs(282107);
                Spawn(730317, 328.476f, 762.585f, 197.479f, (byte)90); // Exit
                break;
            case 700955: // Huge Aether Fragment
                destroyedFragments++;
                OnFragmentKill();
                npc.GetController().Delete();
                break;
            case 281909:
                if (++killedPazuzuWorms == 5)
                {
                    killedPazuzuWorms = 0;
                    Npc pazuzu = GetNpc(216951);
                    if (pazuzu != null && !pazuzu.IsDead())
                    {
                        pazuzu.GetEffectController().RemoveEffect(19145);
                        pazuzu.GetEffectController().RemoveEffect(19291);
                    }
                }
                npc.GetController().Delete();
                break;
            case 282014: // Spawn Gate
            case 282015: // Spawn Gate
            case 282131: // Spawn Gate
                DeleteSummons();
                break;
        }
    }

    public override void OnInstanceDestroy()
    {
        destroyedFragments = 0;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 700862: // Broken Orkanimum
                int itemId = player.GetRace() == Race.ASMODIANS ? 182209820 : 182209800;
                if (player.GetInventory().GetFirstItemByItemId(itemId) == null)
                    ItemService.AddItem(player, itemId, 1);
                break;
            case 700865: // Worn Book
                if (player.GetRace() == Race.ASMODIANS && player.GetInventory().GetFirstItemByItemId(182209824) == null)
                    ItemService.AddItem(player, 182209824, 1);
                break;
            case 700864: // Polearm of Akarios
                if (player.GetRace() == Race.ELYOS && player.GetInventory().GetFirstItemByItemId(182209803) == null)
                    ItemService.AddItem(player, 182209803, 1);
                break;
            case 701593: // Artifact of Protection (Hard Mode)
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdDH_Wakeup());
                Spawn(216960, 329.70886f, 733.8744f, 197.60938f, (byte)0);
                int artifactOfProtection = player.GetRace() == Race.ELYOS ? 700857 : 700858; // for quest 30255 / 30355
                Spawn(artifactOfProtection, 326.1821f, 766.9640f, 202.1832f, (byte)100, 79);
                npc.GetController().Die();
                break;
            }
            case 700856: // Artifact of Protection (Easy Mode)
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_Wakeup());
                Spawn(216952, 329.70886f, 733.8744f, 197.60938f, (byte)0);
                int artifactOfProtection = player.GetRace() == Race.ELYOS ? 700857 : 700858; // for quest 30255 / 30355
                Spawn(artifactOfProtection, 326.1821f, 766.9640f, 202.1832f, (byte)100, 79);
                npc.GetController().Die();
                break;
            }
        }
    }

    private void SpawnPazuzuFragment()
    {
        Spawn(700955, 669.576f, 335.135f, 465.895f, (byte)0);
    }

    private void SpawnPazuzuTreasureBoxes()
    {
        Spawn(700934, 651.53204f, 357.085f, 466.1315f, (byte)66); // Genesis Treasure Box
        Spawn(700934, 647.00446f, 357.2484f, 465.8960f, (byte)0); // Genesis Treasure Box
        Spawn(700934, 653.8384f, 360.39508f, 466.4391f, (byte)100); // Genesis Treasure Box
        Spawn(700860, 649.24286f, 361.33755f, 466.0427f, (byte)33); // Abyssal Treasure Box
        if (Rnd.Chance() < 12)
            Spawn(700861, 661.061f, 357.587f, 465.991f, (byte)100, 67); // Pazuzu's Treasure Box
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
    }

    private void SpawnKaluvaFragment()
    {
        Spawn(700955, 633.7498f, 557.8822f, 424.99347f, (byte)6);
    }

    private void SpawnKaluvaTreasureBoxes()
    {
        Spawn(700934, 601.2931f, 584.66705f, 422.9955f, (byte)6); // Genesis Treasure Box
        Spawn(700934, 597.2156f, 583.95416f, 423.3474f, (byte)66); // Genesis Treasure Box
        Spawn(700934, 602.9586f, 589.2678f, 422.8296f, (byte)100); // Genesis Treasure Box
        Spawn(700935, 598.82776f, 588.25946f, 422.7739f, (byte)113); // Abyssal Treasure Box
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
    }

    private void SpawnDayshadeFragment()
    {
        Spawn(700955, 452.89706f, 692.36084f, 433.96838f, (byte)6);
    }

    private void SpawnDayshadeTreasureBoxes()
    {
        Spawn(700934, 408.10938f, 650.9015f, 439.28332f, (byte)66); // Genesis Treasure Box
        Spawn(700934, 402.40375f, 655.55237f, 439.26288f, (byte)33); // Genesis Treasure Box
        Spawn(700934, 406.74445f, 655.5914f, 439.2548f, (byte)100); // Genesis Treasure Box
        Spawn(700936, 404.891f, 650.2943f, 439.2548f, (byte)130); // Abyssal Treasure Box
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
    }

    private void SpawnYamennesTreasureBoxes(int npcId)
    {
        Spawn(700934, 326.978f, 729.8414f, 197.7078f, (byte)16); // Genesis Treasure Box
        Spawn(700934, 326.5296f, 735.13324f, 197.6681f, (byte)66); // Genesis Treasure Box
        Spawn(700934, 329.8462f, 738.41095f, 197.7329f, (byte)3); // Genesis Treasure Box
        Spawn(npcId, 330.891f, 733.2943f, 197.6404f, (byte)113); // Abyssal Treasure Box
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
    }

    private void DeleteSummons()
    {
        if (instance.GetNpcs(282014, 282015, 282131).All(c => c.IsDead()))
            DeleteAliveNpcs(281903, 281904); // Summoned Orkanimum, Summoned Lapilima
    }

    private void OnFragmentKill()
    {
        switch (destroyedFragments)
        {
            case 1: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_01()); break;
            case 2: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_02()); break;
            case 3:
                DeleteAliveNpcs(700856); // Artifact of Protection (Easy Mode)
                Spawn(701593, 326.1821f, 766.9640f, 202.1832f, (byte)100, 79); // Artifact of Protection (Hard Mode)
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_03());
                break;
        }
    }
}
