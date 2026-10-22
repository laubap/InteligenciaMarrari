namespace IAApi.Services.AnomalyDetection;

public class AnomalyDetectionOptions
{
    // Score mínimo do ensemble para considerar anomalia.
    public double EnsembleThreshold { get; set; } = 60;

    // Quantidade mínima de histórico para análise.
    public int MinHistoryPoints { get; set; } = 30;

    // Pesos dos detectores.
    public double ZScoreWeight { get; set; } = 0.20;
    public double MadWeight { get; set; } = 0.20;
    public double EwmaWeight { get; set; } = 0.20;
    public double RateOfChangeWeight { get; set; } = 0.15;
    public double PersistenceWeight { get; set; } = 0.15;
    public double LevelShiftWeight { get; set; } = 0.10;

    // Z-score tradicional.
    public double ZScoreThreshold { get; set; } = 3.0;

    // Z-score modificado baseado em MAD.
    public double MadThreshold { get; set; } = 3.5;

    // EWMA.
    public double EwmaLambda { get; set; } = 0.25;
    public double EwmaSigmaLimit { get; set; } = 3.0;

    // Taxa de variação.
    public double RateOfChangeThreshold { get; set; } = 3.5;

    // Persistência.
    public int PersistenceMinimumPoints { get; set; } = 5;
    public double PersistenceMinimumMinutes { get; set; } = 1;

    // Experimental e desligado por padrão. Quando ativo, confirmações
    // temporais derivadas de MAD não participam da soma estatística.
    public bool ExperimentalPersistenceTemporalConfirmationEnabled { get; set; }

    // Mudança de patamar.
    public int LevelShiftPreviousWindow { get; set; } = 20;
    public int LevelShiftRecentWindow { get; set; } = 10;
    public double LevelShiftThreshold { get; set; } = 3.0;
    public double LevelShiftMinimumConsistency { get; set; } = 0.80;
}
