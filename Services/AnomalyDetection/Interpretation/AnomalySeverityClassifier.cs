namespace IAApi.Services.AnomalyDetection.Interpretation;

public class AnomalySeverityClassifier
{
    public static string Classificar(
        double scoreNormalizado,
        double limiteEnsemble)
    {
        if (scoreNormalizado < limiteEnsemble * 0.5)
        {
            return "Normal";
        }

        if (scoreNormalizado < limiteEnsemble)
        {
            return "Atenção";
        }

        if (scoreNormalizado < 85)
        {
            return "Alta";
        }

        return "Crítica";
    }
}