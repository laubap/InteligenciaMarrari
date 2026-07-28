namespace IAApi.Services.AnomalyDetection;

public interface IAnomalyDetector
{
    string Nome { get; }

    double Peso { get; }

    DetectorResult Detectar(DetectorInput input);
}