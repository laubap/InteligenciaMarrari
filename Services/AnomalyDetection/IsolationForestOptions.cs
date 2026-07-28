namespace IAApi.Services.AnomalyDetection;

public class IsolationForestOptions
{
    public double Weight { get; set; } = 0.25;

    public int TreeCount { get; set; } = 100;

    public int SampleSize { get; set; } = 256;

    public int MinimumHistoryPoints { get; set; } = 50;

    public int FeatureWindowSize { get; set; } = 10;

    public double AnomalyThreshold { get; set; } = 0.62;

    /*
     * Uma semente fixa torna os testes reproduzíveis.
     * Posteriormente ela poderá ser diferente para cada modelo.
     */
    public int RandomSeed { get; set; } = 42;

    /*
     * Inclinação da função logística usada para converter
     * o score clássico em score de 0 a 100.
     */
    public double NormalizationSlope { get; set; } = 12;
}