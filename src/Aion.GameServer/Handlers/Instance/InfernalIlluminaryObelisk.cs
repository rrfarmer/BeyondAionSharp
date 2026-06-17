using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/InfernalIlluminaryObelisk (Estrayl) : IlluminaryObeliskInstance. @InstanceID(301370000). Overrides spawnEndboss(→234686) and scheduleChargeAttacks (234xxx infernal minion ids, same 4-direction wave layout). 1:1.</summary>
[InstanceID(301370000)]
public class InfernalIlluminaryObelisk : IlluminaryObeliskInstance
{
    public InfernalIlluminaryObelisk(WorldMapInstance instance) : base(instance)
    {
    }

    protected override void SpawnEndboss(int npcId)
    {
        base.SpawnEndboss(234686);
    }

    protected override void ScheduleChargeAttacks(int npcId)
    {
        switch (npcId)
        {
            case 702218: // east first wave
                Spawn(234720, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(234721, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(234721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(234722, 255.3635f, 328.5584f, 325.0038f, (byte)90, 15000, "idf5_u3_east_2");
                Spawn(234720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 15000, "idf5_u3_east_3");
                Spawn(234720, 252.3243f, 328.5881f, 325.0092f, (byte)90, 15000, "idf5_u3_east_4");
                Spawn(234723, 255.3635f, 328.5584f, 325.0038f, (byte)90, 30000, "idf5_u3_east_2");
                Spawn(234726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 30000, "idf5_u3_east_3");
                Spawn(234726, 252.3243f, 328.5881f, 325.0092f, (byte)90, 30000, "idf5_u3_east_4");
                break;
            case 702219: // east second wave
                Spawn(234723, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(234726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(234726, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(234728, 255.3635f, 328.5584f, 325.0038f, (byte)90, 15000, "idf5_u3_east_2");
                Spawn(234721, 258.5159f, 328.5792f, 325.0038f, (byte)90, 15000, "idf5_u3_east_3");
                Spawn(234721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 15000, "idf5_u3_east_4");
                Spawn(234722, 255.3635f, 328.5584f, 325.0038f, (byte)90, 30000, "idf5_u3_east_2");
                Spawn(234720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 30000, "idf5_u3_east_3");
                Spawn(234720, 252.3243f, 328.5881f, 325.0092f, (byte)90, 30000, "idf5_u3_east_4");
                break;
            case 702220: // east third wave
                Spawn(234721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_1");
                Spawn(234726, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(234721, 256.6376f, 328.7015f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(234726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(234682, 253.8757f, 326.5010f, 325.0038f, (byte)90, 0, "idf5_u3_east_6");
                Spawn(234720, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(234724, 256.6376f, 328.7015f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(234720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(234733, 256.9199f, 326.4982f, 325.0038f, (byte)90, 0, "idf5_u3_east_5");
                break;
            case 702221: // west first wave
                Spawn(234720, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(234723, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(234720, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(234721, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(234724, 255.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(234721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");
                Spawn(234722, 253.5314f, 183.5728f, 325.0038f, (byte)30, 30000, "idf5_u3_west_2");
                Spawn(234725, 255.2491f, 183.4584f, 325.0038f, (byte)30, 30000, "idf5_u3_west_3");
                Spawn(234722, 257.0595f, 183.5797f, 325.0045f, (byte)30, 30000, "idf5_u3_west_4");
                break;
            case 702222: // west second wave
                Spawn(234721, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(234720, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(234721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(234726, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(234727, 255.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(234726, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");
                Spawn(234725, 253.5314f, 183.5728f, 325.0038f, (byte)30, 30000, "idf5_u3_west_2");
                Spawn(234732, 255.2491f, 183.4584f, 325.0038f, (byte)30, 30000, "idf5_u3_west_3");
                Spawn(234725, 257.0595f, 183.5797f, 325.0045f, (byte)30, 30000, "idf5_u3_west_4");
                break;
            case 702223: // west third wave
                Spawn(234721, 251.9594f, 183.4159f, 325.0038f, (byte)30, 0, "idf5_u3_west_1");
                Spawn(234722, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(234722, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(234721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(234683, 255.0448f, 185.5452f, 325.0038f, (byte)30, 0, "idf5_u3_west_6");
                Spawn(234725, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(234720, 252.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(234731, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");
                Spawn(234725, 258.7057f, 183.6840f, 325.0038f, (byte)30, 15000, "idf5_u3_west_5");
                break;
            case 702224: // south first wave
                Spawn(234722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(234723, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(234722, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(234725, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(234730, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(234725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(234726, 326.3337f, 252.6159f, 291.8364f, (byte)60, 30000, "idf5_u3_south_2");
                Spawn(234727, 326.3333f, 253.1857f, 291.8364f, (byte)60, 30000, "idf5_u3_south_3");
                Spawn(234726, 326.4392f, 255.9983f, 291.8364f, (byte)60, 30000, "idf5_u3_south_4");
                break;
            case 702225: // south second wave
                Spawn(234722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(234723, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(234722, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(234725, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(234730, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(234725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(234726, 326.3337f, 252.6159f, 291.8364f, (byte)60, 30000, "idf5_u3_south_2");
                Spawn(234727, 326.3333f, 253.1857f, 291.8364f, (byte)60, 30000, "idf5_u3_south_3");
                Spawn(234726, 326.4392f, 255.9983f, 291.8364f, (byte)60, 30000, "idf5_u3_south_4");
                break;
            case 702226: // south third wave
                Spawn(234725, 326.3734f, 251.2209f, 291.8364f, (byte)60, 0, "idf5_u3_south_1");
                Spawn(234720, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(234720, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(234725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(234738, 324.7853f, 254.2962f, 291.8364f, (byte)60, 0, "idf5_u3_south_6");
                Spawn(234722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(234722, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(234684, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(234723, 326.4354f, 257.6836f, 291.8466f, (byte)60, 15000, "idf5_u3_south_5");
                break;
            case 702227: // north first wave
                Spawn(234722, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(234727, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(234722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(234725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(234723, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(234725, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(234725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 30000, "idf5_u3_north_2");
                Spawn(234729, 184.6134f, 253.0914f, 291.8364f, (byte)0, 30000, "idf5_u3_north_3");
                Spawn(234725, 184.6415f, 253.7202f, 291.8364f, (byte)0, 30000, "idf5_u3_north_4");
                Spawn(233882, 253.1755f, 252.6574f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_1");
                Spawn(233883, 253.1821f, 254.5660f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_2");
                Spawn(233882, 253.3598f, 256.3680f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_3");
                break;
            case 702228: // north second wave
                Spawn(234726, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(234723, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(234726, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(234722, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(234724, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(234722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(234720, 184.6565f, 256.3191f, 291.8364f, (byte)0, 30000, "idf5_u3_north_2");
                Spawn(234734, 184.6415f, 253.7202f, 291.8364f, (byte)0, 30000, "idf5_u3_north_3");
                Spawn(234720, 184.6134f, 253.0914f, 291.8364f, (byte)0, 30000, "idf5_u3_north_4");
                break;
            case 702229: // north third wave
                Spawn(234725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_1");
                Spawn(234720, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(234724, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(234725, 184.7428f, 251.3166f, 291.8842f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(234731, 186.8694f, 254.6730f, 291.8364f, (byte)0, 0, "idf5_u3_north_6");
                Spawn(234722, 184.7428f, 251.3166f, 291.8842f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(234721, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(234685, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(234722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_5");
                break;
        }
    }
}
