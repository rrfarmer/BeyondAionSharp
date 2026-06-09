namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/DataSet.</summary>
public class DataSet
{
    private double[] values;
    private double[] targets;

    public DataSet(double[] values, double[] targets)
    {
        this.values = values;
        this.targets = targets;
    }

    public double[] GetValues()
    {
        return values;
    }

    public double[] GetTargets()
    {
        return targets;
    }
}
