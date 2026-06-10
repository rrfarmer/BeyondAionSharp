using System.Collections.Generic;

namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/PlayerModelLink (Jo). Neuron node. ArrayList→List; Double target→double?; doubleValue()→.Value.</summary>
public class PlayerModelLink
{
    public List<Link> inputs;
    public List<Link> outputs;
    public double bias;
    public double biasDelta;
    public double gradient;
    public double value;

    public PlayerModelLink()
    {
        inputs = new List<Link>();
        outputs = new List<Link>();
        bias = PlayerModel.GetRandom();
    }

    public PlayerModelLink(List<PlayerModelLink> inputList) : this()
    {
        foreach (PlayerModelLink input in inputList)
        {
            Link l = new Link(input, this);
            input.outputs.Add(l);
            inputs.Add(l);
        }
    }

    public double CalculateValue()
    {
        double sum = 0;
        foreach (Link l in inputs)
            sum += l.GetWeight() * l.GetInput().value;

        value = Sigmoid.Output(sum + bias);
        return value;
    }

    public double CalculateError(double target)
    {
        return target - value;
    }

    public double CalculateGradient(double? target)
    {
        if (target == null)
        {
            double sum = 0;
            foreach (Link l in outputs)
                sum += l.GetOutput().gradient * l.GetWeight();
            gradient = sum * Sigmoid.Derivative(value);
            return gradient;
        }

        gradient = CalculateError(target.Value) * Sigmoid.Derivative(value);
        return gradient;
    }

    public void UpdateWeights(double learnRate, double momentum)
    {
        double prevDelta = biasDelta;
        biasDelta = learnRate * gradient;
        bias += biasDelta + momentum * prevDelta;

        foreach (Link l in inputs)
        {
            prevDelta = l.GetWeightDelta();
            l.SetWeightDelta(learnRate * gradient * l.GetInput().value);
            l.SetWeight(l.GetWeight() + l.GetWeightDelta() + momentum * prevDelta);
        }
    }
}
