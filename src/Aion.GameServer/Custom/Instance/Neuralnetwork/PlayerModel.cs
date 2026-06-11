using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/PlayerModel (Jo). slf4j "CUSTOM_INSTANCE_LOG"→ILogger named category; Double learnRate/momentum→double? with null defaults; varargs double...→params double[]; forEach(method ref)→List.ForEach(lambda); currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; String.format→string.Format. Rnd red-tolerated.</summary>
public class PlayerModel
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger("CUSTOM_INSTANCE_LOG");
    private const long MAX_TRAINING_TIME_IN_MS = 180000;
    public bool isReady;
    public double learnRate;
    public double momentum;
    public List<PlayerModelLink> input;
    public List<List<PlayerModelLink>> inner;
    public List<PlayerModelLink> output;

    public PlayerModel(int inputSize, int hiddenSize, int outputSize, int innerSize, double? learnRate, double? momentum)
    {
        if (learnRate == null)
            this.learnRate = .4;
        else
            this.learnRate = learnRate.Value;
        if (momentum == null)
            this.momentum = .9;
        else
            this.momentum = momentum.Value;
        input = new List<PlayerModelLink>();
        inner = new List<List<PlayerModelLink>>();
        output = new List<PlayerModelLink>();
        isReady = false;

        for (int i = 0; i < inputSize; i++)
            input.Add(new PlayerModelLink());

        if (innerSize < 1)
            innerSize = 1;
        for (int i = 0; i < innerSize; i++)
        {
            inner.Add(new List<PlayerModelLink>());
            for (int j = 0; j < hiddenSize; j++)
                inner[i].Add(new PlayerModelLink(i == 0 ? input : inner[i - 1]));
        }

        for (int i = 0; i < outputSize; i++)
            output.Add(new PlayerModelLink(inner[innerSize - 1]));
    }

    public void Train(List<DataSet> dataSets, int numEpochs)
    {
        long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        isReady = false;
        for (int i = 0; i < numEpochs; i++)
        {
            foreach (DataSet dataSet in dataSets)
            {
                ProcessInput(dataSet.GetValues());
                ValideToOutput(dataSet.GetTargets());
            }
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= startTime + MAX_TRAINING_TIME_IN_MS)
            {
                numEpochs = i;
                break;
            }
        }
        isReady = true;
        long processingTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
        if (processingTime >= MAX_TRAINING_TIME_IN_MS)
            Log.LogWarning(string.Format("[CI_ROAH] Deep learning exceeded [MAX_TRAINING_TIME_IN_MS={0}] with {1} data sets. Only {2} cycles were processed.",
                MAX_TRAINING_TIME_IN_MS, dataSets.Count, numEpochs));
    }

    private void ProcessInput(params double[] inputs)
    {
        int i = 0;
        foreach (PlayerModelLink n in input)
            n.value = inputs[i++];
        foreach (List<PlayerModelLink> layer in inner)
            layer.ForEach(a => a.CalculateValue());
        output.ForEach(a => a.CalculateValue());
    }

    private void ValideToOutput(params double[] targets)
    {
        int i = 0;
        foreach (PlayerModelLink n in output)
            n.CalculateGradient(targets[i++]);

        for (int j = inner.Count - 1; j >= 0; j--)
        {
            inner[j].ForEach(a => a.CalculateGradient(null));
            inner[j].ForEach(a => a.UpdateWeights(learnRate, momentum));
        }

        output.ForEach(a => a.UpdateWeights(learnRate, momentum));
    }

    public List<double> GetOutputEstimation(params double[] inputs)
    {
        ProcessInput(inputs);

        List<double> outputList = new List<double>();
        foreach (PlayerModelLink n in output)
            outputList.Add(n.value);

        return outputList;
    }

    public static double GetRandom()
    {
        return 2 * Rnd.NextDouble() - 1;
    }
}
