using System.Collections;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark;

/// <summary>
/// Histórico em memória usado pelo benchmark.
///
/// A implementação utiliza um buffer circular e expõe o próprio buffer
/// como IReadOnlyList para os detectores. Dessa forma, CriarSnapshotDetector
/// não copia a janela histórica e não cria milhares de objetos a cada leitura.
///
/// Média e desvio padrão são mantidos incrementalmente em O(1).
///
/// Observação: este serviço foi projetado para o fluxo sequencial do benchmark.
/// O histórico não deve ser alterado enquanto um detector estiver percorrendo
/// a visão retornada no snapshot.
/// </summary>
public class SQLiteHistoricoService : IHistoricoService
{
    private readonly Dictionary<string, CircularHistoryBuffer> _historicos =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly int _limitePorTag;

    public SQLiteHistoricoService(int limitePorTag = 1440)
    {
        if (limitePorTag < 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limitePorTag),
                "O limite do histórico deve possuir pelo menos 10 pontos.");
        }

        _limitePorTag = limitePorTag;
    }

    public void AdicionarLeitura(
        string clienteId,
        string tagName,
        DateTime dataHora,
        float valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        if (!float.IsFinite(valor))
        {
            return;
        }

        var chave = CriarChave(clienteId, tagName);

        if (!_historicos.TryGetValue(chave, out var historico))
        {
            historico = new CircularHistoryBuffer(_limitePorTag);
            _historicos[chave] = historico;
        }

        historico.Add(dataHora, valor);
    }

    /// <summary>
    /// Retorna uma visão somente leitura do buffer circular.
    ///
    /// Não cria uma nova lista e não recria HistoricoDetectorPoint.
    /// Média e desvio são obtidos das estatísticas incrementais do buffer.
    /// </summary>
    public BenchmarkHistorySnapshot CriarSnapshotDetector(
        string clienteId,
        string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var chave = CriarChave(clienteId, tagName);

        if (!_historicos.TryGetValue(chave, out var historico) ||
            historico.Count == 0)
        {
            return BenchmarkHistorySnapshot.Empty;
        }

        return historico.CreateDetectorSnapshot();
    }

    public List<LeituraBruta> BuscarHistorico(
        string clienteId,
        string tagName,
        string tipoTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var chave = CriarChave(clienteId, tagName);

        return _historicos.TryGetValue(chave, out var historico)
            ? historico.ToLeituraBrutaList()
            : [];
    }

    public List<LeituraBruta> BuscarHistoricoPorPeriodo(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        if (fim < inicio)
        {
            throw new ArgumentException(
                "A data final não pode ser anterior à data inicial.");
        }

        var chave = CriarChave(clienteId, tagName);

        return _historicos.TryGetValue(chave, out var historico)
            ? historico.ToLeituraBrutaList(inicio, fim)
            : [];
    }

    public void LimparTag(
        string clienteId,
        string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        _historicos.Remove(
            CriarChave(clienteId, tagName));
    }

    public void LimparTudo()
    {
        _historicos.Clear();
    }

    public int ObterQuantidade(
        string clienteId,
        string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        return _historicos.TryGetValue(
            CriarChave(clienteId, tagName),
            out var historico)
                ? historico.Count
                : 0;
    }

    private static string CriarChave(
        string clienteId,
        string tagName)
    {
        return $"{clienteId.Trim()}::{tagName.Trim()}";
    }
}

public sealed class BenchmarkHistorySnapshot
{
    public static BenchmarkHistorySnapshot Empty { get; } = new()
    {
        Historico = Array.Empty<HistoricoDetectorPoint>(),
        Media = 0,
        DesvioPadrao = 0
    };

    public required IReadOnlyList<HistoricoDetectorPoint>
        Historico { get; init; }

    public required double Media { get; init; }

    public required double DesvioPadrao { get; init; }
}

/// <summary>
/// Buffer circular que também funciona como IReadOnlyList.
///
/// Os objetos HistoricoDetectorPoint são alocados no máximo uma vez por
/// posição do buffer e depois reutilizados. Quando a janela está cheia,
/// o ponto mais antigo é sobrescrito sem deslocar os demais itens.
/// </summary>
internal sealed class CircularHistoryBuffer :
    IReadOnlyList<HistoricoDetectorPoint>
{
    private readonly HistoricoDetectorPoint?[] _pontos;

    private int _inicio;

    /*
     * Estatísticas de Welford para os pontos atualmente presentes
     * na janela. Elas são atualizadas ao inserir, substituir ou remover
     * implicitamente o ponto mais antigo.
     */
    private int _quantidadeEstatistica;
    private double _media;
    private double _m2;

    public CircularHistoryBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity));
        }

        _pontos =
            new HistoricoDetectorPoint?[capacity];
    }

    public int Count { get; private set; }

    private int Capacity =>
        _pontos.Length;

    public HistoricoDetectorPoint this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            var indiceFisico =
                (_inicio + index) % Capacity;

            return _pontos[indiceFisico]
                ?? throw new InvalidOperationException(
                    "O buffer contém uma posição não inicializada.");
        }
    }

    public double Media =>
        _quantidadeEstatistica > 0
            ? _media
            : 0;

    public double DesvioPadrao
    {
        get
        {
            if (_quantidadeEstatistica <= 0)
            {
                return 0;
            }

            var variancia =
                _m2 / _quantidadeEstatistica;

            return Math.Sqrt(
                Math.Max(0, variancia));
        }
    }

    public void Add(
        DateTime dataHora,
        float valor)
    {
        if (!float.IsFinite(valor))
        {
            return;
        }

        /*
         * Se a última leitura possui o mesmo timestamp, atualiza o ponto
         * sem aumentar a janela. As estatísticas retiram o valor antigo
         * e incluem o novo.
         */
        if (Count > 0)
        {
            var ultimoIndice =
                (_inicio + Count - 1) % Capacity;

            var ultimoPonto =
                _pontos[ultimoIndice];

            if (ultimoPonto is not null &&
                ultimoPonto.DataHora == dataHora)
            {
                RemoverDasEstatisticas(
                    ultimoPonto.Valor);

                ultimoPonto.Valor =
                    valor;

                AdicionarNasEstatisticas(
                    valor);

                return;
            }
        }

        if (Count < Capacity)
        {
            var indice =
                (_inicio + Count) % Capacity;

            var ponto =
                _pontos[indice];

            if (ponto is null)
            {
                ponto = new HistoricoDetectorPoint();
                _pontos[indice] = ponto;
            }

            ponto.DataHora =
                dataHora;

            ponto.Valor =
                valor;

            Count++;

            AdicionarNasEstatisticas(
                valor);

            return;
        }

        /*
         * Janela cheia: reutiliza o objeto da posição mais antiga.
         */
        var pontoSobrescrito =
            _pontos[_inicio]
            ?? throw new InvalidOperationException(
                "A posição mais antiga do buffer não foi inicializada.");

        RemoverDasEstatisticas(
            pontoSobrescrito.Valor);

        pontoSobrescrito.DataHora =
            dataHora;

        pontoSobrescrito.Valor =
            valor;

        AdicionarNasEstatisticas(
            valor);

        _inicio =
            (_inicio + 1) % Capacity;
    }

    public BenchmarkHistorySnapshot CreateDetectorSnapshot()
    {
        /*
         * Historico aponta para o próprio buffer.
         * Não há cópia da janela nem criação de pontos.
         */
        return new BenchmarkHistorySnapshot
        {
            Historico = this,
            Media = Media,
            DesvioPadrao = DesvioPadrao
        };
    }

    public List<LeituraBruta> ToLeituraBrutaList()
    {
        var resultado =
            new List<LeituraBruta>(Count);

        for (var i = 0;
             i < Count;
             i++)
        {
            var ponto = this[i];

            resultado.Add(
                new LeituraBruta
                {
                    DataHora =
                        ponto.DataHora,
                    Valor =
                        ConverterParaFloatSeguro(ponto.Valor)
                });
        }

        return resultado;
    }

    public List<LeituraBruta> ToLeituraBrutaList(
        DateTime inicio,
        DateTime fim)
    {
        var resultado =
            new List<LeituraBruta>();

        for (var i = 0;
             i < Count;
             i++)
        {
            var ponto = this[i];

            if (ponto.DataHora < inicio ||
                ponto.DataHora > fim)
            {
                continue;
            }

            resultado.Add(
                new LeituraBruta
                {
                    DataHora =
                        ponto.DataHora,
                    Valor =
                        ConverterParaFloatSeguro(ponto.Valor)
                });
        }

        return resultado;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<HistoricoDetectorPoint>
        IEnumerable<HistoricoDetectorPoint>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    private void AdicionarNasEstatisticas(
        double valor)
    {
        _quantidadeEstatistica++;

        var delta =
            valor - _media;

        _media +=
            delta / _quantidadeEstatistica;

        var delta2 =
            valor - _media;

        _m2 +=
            delta * delta2;

        CorrigirErroNumerico();
    }

    private void RemoverDasEstatisticas(
        double valor)
    {
        if (_quantidadeEstatistica <= 1)
        {
            _quantidadeEstatistica = 0;
            _media = 0;
            _m2 = 0;
            return;
        }

        var quantidadeAnterior =
            _quantidadeEstatistica;

        var novaQuantidade =
            quantidadeAnterior - 1;

        var novaMedia =
            ((quantidadeAnterior * _media) - valor) /
            novaQuantidade;

        /*
         * Fórmula reversa de Welford para retirar um ponto.
         */
        _m2 -=
            (valor - _media) *
            (valor - novaMedia);

        _media =
            novaMedia;

        _quantidadeEstatistica =
            novaQuantidade;

        CorrigirErroNumerico();
    }

    private void CorrigirErroNumerico()
    {
        /*
         * Pequenos valores negativos podem surgir por arredondamento.
         */
        if (_m2 < 0 &&
            _m2 > -1e-9)
        {
            _m2 = 0;
        }

        if (!double.IsFinite(_media) ||
            !double.IsFinite(_m2))
        {
            RecalcularEstatisticas();
        }
    }

    private void RecalcularEstatisticas()
    {
        _quantidadeEstatistica = 0;
        _media = 0;
        _m2 = 0;

        for (var i = 0;
             i < Count;
             i++)
        {
            var valor =
                this[i].Valor;

            if (!double.IsFinite(valor))
            {
                continue;
            }

            _quantidadeEstatistica++;

            var delta =
                valor - _media;

            _media +=
                delta / _quantidadeEstatistica;

            var delta2 =
                valor - _media;

            _m2 +=
                delta * delta2;
        }

        if (_m2 < 0)
        {
            _m2 = 0;
        }
    }

    private static float ConverterParaFloatSeguro(
        double valor)
    {
        if (!double.IsFinite(valor))
        {
            return 0;
        }

        return (float)Math.Clamp(
            valor,
            float.MinValue,
            float.MaxValue);
    }

    public struct Enumerator :
        IEnumerator<HistoricoDetectorPoint>
    {
        private readonly CircularHistoryBuffer _buffer;

        private int _index;

        internal Enumerator(
            CircularHistoryBuffer buffer)
        {
            _buffer = buffer;
            _index = -1;
            Current = null!;
        }

        public HistoricoDetectorPoint Current
        {
            get;
            private set;
        }

        object IEnumerator.Current =>
            Current;

        public bool MoveNext()
        {
            var proximoIndice =
                _index + 1;

            if (proximoIndice >=
                _buffer.Count)
            {
                return false;
            }

            _index =
                proximoIndice;

            Current =
                _buffer[_index];

            return true;
        }

        public void Reset()
        {
            _index = -1;
            Current = null!;
        }

        public void Dispose()
        {
        }
    }
}