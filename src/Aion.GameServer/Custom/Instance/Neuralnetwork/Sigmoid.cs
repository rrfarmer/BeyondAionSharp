using System;

namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/Sigmoid.</summary>
public class Sigmoid
{
    public static double Output(double x)
    {
        return x < -45.0 ? 0.0 : x > 45.0 ? 1.0 : 1.0 / (1.0 + Math.Exp((float) -x));
    }

    public static double Derivative(double x)
    {
        return x * (1 - x);
    }
}
