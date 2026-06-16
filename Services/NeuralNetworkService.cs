using System;

namespace APP.Services;

public class NeuralNetworkService
{
    private readonly Random _random = new(42); 
    private readonly double[,] _weightsInputToHidden = new double[3, 4];
    private readonly double[] _biasHidden = new double[4];
    private readonly double[,] _weightsHiddenToOutput = new double[4, 1];
    private readonly double[] _biasOutput = new double[1];

    public NeuralNetworkService()
    {
        InitializeWeights();
    }

    private void InitializeWeights()
    {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 4; j++)
                _weightsInputToHidden[i, j] = (_random.NextDouble() * 2.0 - 1.0) * Math.Sqrt(2.0 / 3.0);

        for (int i = 0; i < 4; i++)
            _biasHidden[i] = _random.NextDouble() * 0.1;

        for (int i = 0; i < 4; i++)
            _weightsHiddenToOutput[i, 0] = (_random.NextDouble() * 2.0 - 1.0) * Math.Sqrt(2.0 / 4.0);

        _biasOutput[0] = _random.NextDouble() * 0.1;
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));
    private static double SigmoidDerivative(double x) => x * (1.0 - x);

    public double Predict(double areaPercent, double confidence, double windSpeed)
    {
        double normArea = areaPercent / 100.0;
        double normWind = windSpeed / 100.0;

        double[] inputs = [normArea, confidence, normWind];
        double[] hidden = new double[4];

        for (int j = 0; j < 4; j++)
        {
            double sum = 0;
            for (int i = 0; i < 3; i++)
            {
                sum += inputs[i] * _weightsInputToHidden[i, j];
            }
            hidden[j] = Sigmoid(sum + _biasHidden[j]);
        }

        double outSum = 0;
        for (int j = 0; j < 4; j++)
        {
            outSum += hidden[j] * _weightsHiddenToOutput[j, 0];
        }
        double output = Sigmoid(outSum + _biasOutput[0]);

        return output * 50.0;
    }

    public double Train(double[] inputs, double[] targets, double learningRate)
    {
        double[] hidden = new double[4];
        double[] hiddenRaw = new double[4];

        for (int j = 0; j < 4; j++)
        {
            double sum = 0;
            for (int i = 0; i < 3; i++)
            {
                sum += inputs[i] * _weightsInputToHidden[i, j];
            }
            hiddenRaw[j] = sum + _biasHidden[j];
            hidden[j] = Sigmoid(hiddenRaw[j]);
        }

        double outSum = 0;
        for (int j = 0; j < 4; j++)
        {
            outSum += hidden[j] * _weightsHiddenToOutput[j, 0];
        }
        double output = Sigmoid(outSum + _biasOutput[0]);

        double error = targets[0] - output;
        double mse = 0.5 * error * error;

        double dOutput = error * SigmoidDerivative(output);

        double[] dHidden = new double[4];
        for (int j = 0; j < 4; j++)
        {
            dHidden[j] = dOutput * _weightsHiddenToOutput[j, 0] * SigmoidDerivative(hidden[j]);
        }

        for (int j = 0; j < 4; j++)
        {
            _weightsHiddenToOutput[j, 0] += learningRate * dOutput * hidden[j];
        }
        _biasOutput[0] += learningRate * dOutput;

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                _weightsInputToHidden[i, j] += learningRate * dHidden[j] * inputs[i];
            }
        }
        for (int j = 0; j < 4; j++)
        {
            _biasHidden[j] += learningRate * dHidden[j];
        }

        return mse;
    }
}