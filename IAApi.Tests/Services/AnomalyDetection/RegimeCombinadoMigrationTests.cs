using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

/// <summary>
/// RC-5 (PLANO_REGIME_COMBINADO.md §6.3): entidade TagRegimeComponenteIa,
/// coluna MapaEstados e migration RC5_AddTagRegimeComponente.
/// Tudo em SQLite em memória: nenhum banco real é aberto.
/// </summary>
public class RegimeCombinadoMigrationTests
{
    private const string MigracaoRc5 = "RC5_AddTagRegimeComponente";
    private const string MigracaoAnterior = "20261002183911_B325_AddRichBehaviorSnapshotIdentity";

    [Fact]
    public void Modelo_NaoTemMudancasPendentesEmRelacaoAoSnapshot()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void MigracaoRc5_EhAUltimaEVemDepoisDaAnterior()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);

        var migracoes = context.Database.GetMigrations().ToList();

        Assert.EndsWith("_" + MigracaoRc5, migracoes[^1]);
        Assert.Equal(MigracaoAnterior, migracoes[^2]);
    }

    [Fact]
    public void Up_EmSqliteVazio_CriaTabelaColunaIndiceERestricoes()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);

        context.Database.Migrate();

        Assert.Empty(context.Database.GetPendingMigrations());
        Assert.True(TabelaExiste(connection, "TagsRegimesComponentesIa"));
        Assert.Contains("MapaEstados", Colunas(connection, "TagsRegimesOperacionaisIa"));
        Assert.Equal(
            new[] { "ConfiguracaoId", "Id", "MapaEstados", "Ordem", "TagRegime", "ToleranciaEstado" },
            Colunas(connection, "TagsRegimesComponentesIa").OrderBy(c => c, StringComparer.Ordinal));
        Assert.True(IndiceExiste(connection, "IX_TagsRegimesComponentesIa_ConfiguracaoId_Ordem"));
        Assert.True(IndiceExiste(connection, "IX_TagsRegimesOperacionaisIa_ClienteId_TagName"));

        // Tabela nova vazia; a configuração atual continua válida sozinha.
        Assert.Empty(context.TagsRegimesComponentesIa);

        var configuracao = NovaConfiguracao();
        context.TagsRegimesOperacionaisIa.Add(configuracao);
        context.SaveChanges();
        Assert.Null(context.TagsRegimesOperacionaisIa.Single().MapaEstados);

        context.TagsRegimesComponentesIa.AddRange(
            new TagRegimeComponenteIa { ConfiguracaoId = configuracao.Id, Ordem = 2, TagRegime = "TAG43", MapaEstados = "2:2;*:0" },
            new TagRegimeComponenteIa { ConfiguracaoId = configuracao.Id, Ordem = 3, TagRegime = "TAG46", MapaEstados = "380:380;*:450" });
        context.SaveChanges();

        var lidos = context.TagsRegimesComponentesIa.OrderBy(c => c.Ordem).ToList();
        Assert.Equal(new[] { "TAG43", "TAG46" }, lidos.Select(c => c.TagRegime));
        Assert.Equal(0.001, lidos[0].ToleranciaEstado);
    }

    [Fact]
    public void Up_RecusaOrdemMenorQue2_OrdemRepetida_EConfiguracaoInexistente()
    {
        using var connection = AbrirConexao();
        using (var setup = CriarContexto(connection))
        {
            setup.Database.Migrate();
            setup.TagsRegimesOperacionaisIa.Add(NovaConfiguracao());
            setup.SaveChanges();
        }

        int configuracaoId;
        using (var leitura = CriarContexto(connection))
        {
            configuracaoId = leitura.TagsRegimesOperacionaisIa.Single().Id;
        }

        // Ordem 1 é a TagRegime da própria configuração (opção (a)).
        AssertFalhaAoGravar(connection, new TagRegimeComponenteIa { ConfiguracaoId = configuracaoId, Ordem = 1, TagRegime = "TAG43" });
        AssertFalhaAoGravar(connection, new TagRegimeComponenteIa { ConfiguracaoId = configuracaoId, Ordem = 0, TagRegime = "TAG43" });
        AssertFalhaAoGravar(connection, new TagRegimeComponenteIa { ConfiguracaoId = configuracaoId + 999, Ordem = 2, TagRegime = "TAG43" });

        using (var ok = CriarContexto(connection))
        {
            ok.TagsRegimesComponentesIa.Add(new TagRegimeComponenteIa { ConfiguracaoId = configuracaoId, Ordem = 2, TagRegime = "TAG43" });
            ok.SaveChanges();
        }

        AssertFalhaAoGravar(connection, new TagRegimeComponenteIa { ConfiguracaoId = configuracaoId, Ordem = 2, TagRegime = "TAG46" });
    }

    [Fact]
    public void Up_RemoverConfiguracao_RemoveOsComponentes()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);
        context.Database.Migrate();

        var configuracao = NovaConfiguracao();
        context.TagsRegimesOperacionaisIa.Add(configuracao);
        context.SaveChanges();
        context.TagsRegimesComponentesIa.Add(
            new TagRegimeComponenteIa { ConfiguracaoId = configuracao.Id, Ordem = 2, TagRegime = "TAG43" });
        context.SaveChanges();

        context.TagsRegimesOperacionaisIa.Remove(configuracao);
        context.SaveChanges();

        Assert.Equal(0L, Contar(connection, "TagsRegimesComponentesIa"));
    }

    [Fact]
    public void Down_VoltaAoEsquemaAnterior_PreservaConfiguracoes_EUpDeNovoFunciona()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);
        var migrator = context.GetService<IMigrator>();

        context.Database.Migrate();

        var configuracao = NovaConfiguracao();
        configuracao.MapaEstados = "2:2;*:0";
        context.TagsRegimesOperacionaisIa.Add(configuracao);
        context.SaveChanges();
        context.TagsRegimesComponentesIa.Add(
            new TagRegimeComponenteIa { ConfiguracaoId = configuracao.Id, Ordem = 2, TagRegime = "TAG43" });
        context.SaveChanges();

        // Down
        migrator.Migrate(MigracaoAnterior);

        Assert.False(TabelaExiste(connection, "TagsRegimesComponentesIa"));
        var colunasDepoisDoDown = Colunas(connection, "TagsRegimesOperacionaisIa");
        Assert.DoesNotContain("MapaEstados", colunasDepoisDoDown);
        Assert.Equal(
            new[]
            {
                "Ativo", "ClienteId", "DataAtualizacao", "DataCriacao", "Id", "LagMinutos",
                "MinimoAmostrasPorRegime", "TagName", "TagRegime", "ToleranciaAlinhamentoMinutos", "ToleranciaEstado",
            },
            colunasDepoisDoDown.OrderBy(c => c, StringComparer.Ordinal));
        Assert.True(IndiceExiste(connection, "IX_TagsRegimesOperacionaisIa_ClienteId_TagName"));
        Assert.Equal(1L, Contar(connection, "TagsRegimesOperacionaisIa"));
        Assert.Equal(
            "TAG42",
            Escalar(connection, "SELECT TagRegime FROM TagsRegimesOperacionaisIa WHERE TagName = 'TAG29'"));
        Assert.DoesNotContain(
            context.Database.GetAppliedMigrations(),
            m => m.EndsWith("_" + MigracaoRc5, StringComparison.Ordinal));

        // Up de novo
        migrator.Migrate();

        Assert.Empty(context.Database.GetPendingMigrations());
        Assert.True(TabelaExiste(connection, "TagsRegimesComponentesIa"));
        Assert.Equal(0L, Contar(connection, "TagsRegimesComponentesIa"));
        Assert.Equal(
            DBNull.Value,
            EscalarBruto(connection, "SELECT MapaEstados FROM TagsRegimesOperacionaisIa WHERE TagName = 'TAG29'"));
    }

    [Fact]
    public void EnsureCreated_ComoNoReplay_CriaATabelaVazia()
    {
        using var connection = AbrirConexao();
        using var context = CriarContexto(connection);

        context.Database.EnsureCreated();

        Assert.True(TabelaExiste(connection, "TagsRegimesComponentesIa"));
        Assert.Contains("MapaEstados", Colunas(connection, "TagsRegimesOperacionaisIa"));
        Assert.Empty(context.TagsRegimesComponentesIa);
    }

    private static TagRegimeOperacionalIa NovaConfiguracao() => new()
    {
        ClienteId = "CLIENTE-TESTE",
        TagName = "TAG29",
        TagRegime = "TAG42",
        Ativo = true,
        LagMinutos = 0,
        ToleranciaAlinhamentoMinutos = 2,
        ToleranciaEstado = 0.001,
        MinimoAmostrasPorRegime = 30,
        DataCriacao = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc),
    };

    private static SqliteConnection AbrirConexao()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static AppDbContext CriarContexto(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static void AssertFalhaAoGravar(SqliteConnection connection, TagRegimeComponenteIa componente)
    {
        using var context = CriarContexto(connection);
        context.TagsRegimesComponentesIa.Add(componente);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    private static bool TabelaExiste(SqliteConnection connection, string tabela) =>
        Convert.ToInt64(EscalarBruto(
            connection,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{tabela}'")) == 1;

    private static bool IndiceExiste(SqliteConnection connection, string indice) =>
        Convert.ToInt64(EscalarBruto(
            connection,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{indice}'")) == 1;

    private static List<string> Colunas(SqliteConnection connection, string tabela)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{tabela}')";
        using var reader = command.ExecuteReader();
        var colunas = new List<string>();
        while (reader.Read())
        {
            colunas.Add(reader.GetString(0));
        }

        return colunas;
    }

    private static long Contar(SqliteConnection connection, string tabela) =>
        Convert.ToInt64(EscalarBruto(connection, $"SELECT COUNT(*) FROM \"{tabela}\""));

    private static string? Escalar(SqliteConnection connection, string sql) =>
        EscalarBruto(connection, sql)?.ToString();

    private static object? EscalarBruto(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
