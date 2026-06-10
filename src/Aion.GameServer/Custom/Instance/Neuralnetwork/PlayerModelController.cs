using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/PlayerModelController (Jo). ArrayList→List; executeLongRunning(Runnable)→ExecuteLongRunning(async delegate). CustomInstanceService/PlayerModelEntry red-tolerated.</summary>
public class PlayerModelController
{
    private const int TRAINING_EPOCHS = 1000;

    public static PlayerModel TrainModelForPlayer(int playerId, List<int> skillSet)
    {
        // get input data:
        List<PlayerModelEntry> playerModelEntries = CustomInstanceService.GetInstance().GetPlayerModelEntries(playerId);

        // specify model:
        List<DataSet> dataSets = new List<DataSet>();

        for (int i = 0; i < playerModelEntries.Count; i++)
        {
            int previousSkillID = -1;
            if (i > 0)
                previousSkillID = playerModelEntries[i - 1].GetSkillID();
            dataSets.Add(
                new DataSet(playerModelEntries[i].ToStateInputArray(skillSet, previousSkillID), playerModelEntries[i].ToActionOutputArray(skillSet)));
        }

        if (dataSets.Count == 0)
            return null;

        PlayerModel model = new PlayerModel(dataSets[0].GetValues().Length, 10, dataSets[0].GetTargets().Length, 1, null, null);

        // train
        ThreadPoolManager.GetInstance().ExecuteLongRunning(ct => { model.Train(dataSets, TRAINING_EPOCHS); return ValueTask.CompletedTask; });
        return model;
    }

    public static List<int> GetSkillSetForPlayer(int playerId)
    {
        List<int> skillSet = new List<int>();
        foreach (PlayerModelEntry pme in CustomInstanceService.GetInstance().GetPlayerModelEntries(playerId))
            if (!skillSet.Contains(pme.GetSkillID()))
                skillSet.Add(pme.GetSkillID());

        return skillSet;
    }

    public static int GetActionOutput(PlayerModel model, double[] inputArray)
    {
        List<double> targetValues = model.GetOutputEstimation(inputArray);
        return GetMaxIndex(targetValues);
    }

    public static int GetMaxIndex(List<double> values)
    {
        int i = 0;
        int actionI = 0;
        double max_t = 0;

        foreach (double t in values)
        {
            if (t > max_t)
            {
                max_t = t;
                actionI = i;
            }
            i++;
        }
        return actionI;
    }
}
