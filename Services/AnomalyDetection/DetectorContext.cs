namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Contexto calculado uma única vez para uma leitura.
///
/// Os valores e estatísticas derivadas são carregados somente quando
/// algum detector solicita a informação. Depois disso, o resultado fica
/// em cache e pode ser reutilizado pelos demais detectores da mesma
/// execução do ensemble.
///
/// Esta classe não altera a matemática dos detectores.
/// </summary>
public sealed class DetectorContext
{
    private readonly IReadOnlyList<HistoricoDetectorPoint> _historico;
    private readonly DetectionInputQuality _qualidadeHistorico;

    private double[]? _valores;
    private double[]? _taxasHistoricas;

    private bool _medianaCalculada;
    private double _mediana;

    private bool _madCalculado;
    private double _mad;

    private bool _desvioPadraoCalculado;
    private double _desvioPadrao;

    private bool _intervaloMedianoCalculado;
    private double _intervaloMedianoMinutos;

    private bool _medianaTaxasCalculada;
    private double _medianaTaxas;

    private bool _madTaxasCalculado;
    private double _madTaxas;

    private bool _desvioPadraoTaxasCalculado;
    private double _desvioPadraoTaxas;

    public DetectorContext(
        IReadOnlyList<HistoricoDetectorPoint>? historico,
        DetectionInputQuality? qualidadeHistorico = null)
    {
        _historico =
            historico ??
            Array.Empty<HistoricoDetectorPoint>();

        _qualidadeHistorico =
            qualidadeHistorico ??
            DetectionInputQuality.Empty;
    }

    public IReadOnlyList<HistoricoDetectorPoint> Historico =>
        _historico;

    /// <summary>
    /// Qualidade calculada durante a sanitização central do histórico.
    /// Detectores temporais poderão usar estes dados para decidir se um
    /// gap de aquisição invalida uma confirmação.
    /// </summary>
    public DetectionInputQuality QualidadeHistorico =>
        _qualidadeHistorico;

    public int Count =>
        _historico.Count;

    public HistoricoDetectorPoint? UltimoPonto =>
        _historico.Count > 0
            ? _historico[^1]
            : null;

    public double[] Valores
    {
        get
        {
            if (_valores is not null)
            {
                return _valores;
            }

            var temporario =
                new double[_historico.Count];

            var quantidade = 0;

            for (var i = 0;
                 i < _historico.Count;
                 i++)
            {
                var valor =
                    _historico[i].Valor;

                if (!double.IsFinite(valor))
                {
                    continue;
                }

                temporario[quantidade++] =
                    valor;
            }

            if (quantidade != temporario.Length)
            {
                Array.Resize(
                    ref temporario,
                    quantidade);
            }

            _valores =
                temporario;

            return _valores;
        }
    }

    public double Mediana
    {
        get
        {
            if (!_medianaCalculada)
            {
                _mediana =
                    DetectorMath.Median(
                        Valores);

                _medianaCalculada =
                    true;
            }

            return _mediana;
        }
    }

    public double Mad
    {
        get
        {
            if (!_madCalculado)
            {
                _mad =
                    DetectorMath.Mad(
                        Valores,
                        Mediana);

                _madCalculado =
                    true;
            }

            return _mad;
        }
    }

    public double DesvioPadrao
    {
        get
        {
            if (!_desvioPadraoCalculado)
            {
                _desvioPadrao =
                    DetectorMath.DesvioPadrao(
                        Valores);

                _desvioPadraoCalculado =
                    true;
            }

            return _desvioPadrao;
        }
    }

    public double EscalaRobusta
    {
        get
        {
            var escala =
                Mad * 1.4826;

            if (escala > 0 &&
                double.IsFinite(escala))
            {
                return escala;
            }

            return DesvioPadrao;
        }
    }

    public double IntervaloMedianoMinutos
    {
        get
        {
            if (_intervaloMedianoCalculado)
            {
                return _intervaloMedianoMinutos;
            }

            _intervaloMedianoMinutos =
                CalcularIntervaloMedianoOtimizado();

            _intervaloMedianoCalculado =
                true;

            return _intervaloMedianoMinutos;
        }
    }

    public double[] TaxasHistoricas
    {
        get
        {
            if (_taxasHistoricas is not null)
            {
                return _taxasHistoricas;
            }

            if (_historico.Count < 2)
            {
                _taxasHistoricas =
                    Array.Empty<double>();

                return _taxasHistoricas;
            }

            var temporario =
                new double[
                    _historico.Count - 1];

            var quantidade = 0;

            for (var i = 1;
                 i < _historico.Count;
                 i++)
            {
                var anterior =
                    _historico[i - 1];

                var atual =
                    _historico[i];

                var minutos =
                    (atual.DataHora -
                     anterior.DataHora)
                    .TotalMinutes;

                if (minutos <= 0 ||
                    !double.IsFinite(minutos))
                {
                    continue;
                }

                var taxa =
                    (atual.Valor -
                     anterior.Valor) /
                    minutos;

                if (!double.IsFinite(taxa))
                {
                    continue;
                }

                temporario[quantidade++] =
                    taxa;
            }

            if (quantidade != temporario.Length)
            {
                Array.Resize(
                    ref temporario,
                    quantidade);
            }

            _taxasHistoricas =
                temporario;

            return _taxasHistoricas;
        }
    }

    public double MedianaTaxas
    {
        get
        {
            if (!_medianaTaxasCalculada)
            {
                _medianaTaxas =
                    DetectorMath.Median(
                        TaxasHistoricas);

                _medianaTaxasCalculada =
                    true;
            }

            return _medianaTaxas;
        }
    }

    public double MadTaxas
    {
        get
        {
            if (!_madTaxasCalculado)
            {
                _madTaxas =
                    DetectorMath.Mad(
                        TaxasHistoricas,
                        MedianaTaxas);

                _madTaxasCalculado =
                    true;
            }

            return _madTaxas;
        }
    }

    public double DesvioPadraoTaxas
    {
        get
        {
            if (!_desvioPadraoTaxasCalculado)
            {
                _desvioPadraoTaxas =
                    DetectorMath.DesvioPadrao(
                        TaxasHistoricas);

                _desvioPadraoTaxasCalculado =
                    true;
            }

            return _desvioPadraoTaxas;
        }
    }

    public double EscalaRobustaTaxas
    {
        get
        {
            if (MadTaxas > 0 &&
                double.IsFinite(MadTaxas))
            {
                return MadTaxas;
            }

            return DesvioPadraoTaxas;
        }
    }

    private double CalcularIntervaloMedianoOtimizado()
    {
        if (_historico.Count < 2)
        {
            return 0;
        }

        var primeiroIntervaloValido = 0.0;
        var encontrouPrimeiro = false;
        var todosIguais = true;

        for (var i = 1;
             i < _historico.Count;
             i++)
        {
            var minutos =
                (_historico[i].DataHora -
                 _historico[i - 1].DataHora)
                .TotalMinutes;

            if (minutos <= 0 ||
                !double.IsFinite(minutos))
            {
                continue;
            }

            if (!encontrouPrimeiro)
            {
                primeiroIntervaloValido =
                    minutos;

                encontrouPrimeiro =
                    true;

                continue;
            }

            if (Math.Abs(
                    minutos -
                    primeiroIntervaloValido) >
                1e-12)
            {
                todosIguais =
                    false;

                break;
            }
        }

        if (!encontrouPrimeiro)
        {
            return 0;
        }

        if (todosIguais)
        {
            return primeiroIntervaloValido;
        }

        return DetectorMath
            .IntervaloMedianoMinutos(
                _historico);
    }
}