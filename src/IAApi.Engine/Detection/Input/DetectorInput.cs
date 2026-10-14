namespace IAApi.Services.AnomalyDetection;

public class DetectorInput
{
    private IReadOnlyList<HistoricoDetectorPoint> _historico =
        Array.Empty<HistoricoDetectorPoint>();

    private DetectionInputQuality _qualidadeHistorico =
        DetectionInputQuality.Empty;

    private DetectorContext? _contexto;

    private TagBehaviorProfile? _perfilComportamental;

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public string TipoTag { get; set; } = "";

    public double ValorAtual { get; set; }

    public DateTime DataHoraAtual { get; set; } =
        DateTime.Now;

    public double MediaHistorica { get; set; }

    public double DesvioPadraoHistorico { get; set; }

    /// <summary>
    /// Deadband/variação mínima relevante já configurado para a tag.
    ///
    /// Pode vir do valor aprovado pelo cliente ou do perfil treinado.
    /// Quando não for informado (>0), a A3 calcula um fallback automático
    /// a partir do P95 das variações consecutivas do histórico.
    /// </summary>
    public double VariacaoMinimaRelevante { get; set; }

    /// <summary>
    /// Confiança do deadband. Aceita 0..1 ou 0..100 durante a migração.
    /// </summary>
    public double ConfiancaVariacaoMinima { get; set; }

    /// <summary>
    /// Permite distinguir um deadband aprovado pelo cliente de uma
    /// sugestão automática do perfil.
    /// </summary>
    public bool VariacaoMinimaDefinidaPeloCliente { get; set; }

    /// <summary>
    /// Histórico sanitizado e cronológico usado por todos os detectores.
    /// </summary>
    public IReadOnlyList<HistoricoDetectorPoint> Historico
    {
        get => _historico;

        set
        {
            var result =
                DetectionHistorySanitizer.Sanitize(
                    value);

            _historico =
                result.History;

            _qualidadeHistorico =
                result.Quality;

            _contexto = null;
            _perfilComportamental = null;
        }
    }

    public DetectionInputQuality QualidadeHistorico =>
        _qualidadeHistorico;

    public TagBehaviorProfile ObterPerfilComportamental(
        TagBehaviorProfileResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        return _perfilComportamental ??=
            resolver.Resolve(this);
    }

    public DetectorContext Contexto =>
        _contexto ??=
            new DetectorContext(
                _historico,
                _qualidadeHistorico);
}
