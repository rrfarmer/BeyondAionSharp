namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/Link (Jo). Neural-net weighted link. PlayerModelLink/PlayerModel (same package) red-tolerated.</summary>
public class Link
{
    private PlayerModelLink input;
    private PlayerModelLink output;
    private double weight;
    private double weightDelta;

    public Link(PlayerModelLink input, PlayerModelLink output)
    {
        this.input = input;
        this.output = output;
        weight = PlayerModel.GetRandom();
    }

    public PlayerModelLink GetInput()
    {
        return input;
    }

    public PlayerModelLink GetOutput()
    {
        return output;
    }

    public double GetWeight()
    {
        return weight;
    }

    public void SetWeight(double weight)
    {
        this.weight = weight;
    }

    public double GetWeightDelta()
    {
        return weightDelta;
    }

    public void SetWeightDelta(double weightDelta)
    {
        this.weightDelta = weightDelta;
    }
}
