public sealed class MonitoramentoTagsBatchPlannerTests
{
    [Fact]
    public void CriarLotes_SeparaClientesESolicitaTodasAsTagsDoCliente()
    {
        var entradas = new[]
        {
            new TagIaConfig { ClienteId = "PK2", TagName = "PK2.TAG27", IaAtiva = true },
            new TagIaConfig { ClienteId = "OUTRO", TagName = "OUTRO.TAG1", IaAtiva = true },
            new TagIaConfig { ClienteId = "PK2", TagName = "PK2.TAG29", IaAtiva = true },
            new TagIaConfig { ClienteId = "PK2", TagName = "PK2.TAG31", IaAtiva = true },
        };

        var lotes = MonitoramentoTagsBatchPlanner.CriarLotes(entradas);

        Assert.Equal(2, lotes.Count);
        var pk2 = Assert.Single(lotes, lote => lote.ClienteId == "PK2");
        Assert.Equal(new[] { "PK2.TAG27", "PK2.TAG29", "PK2.TAG31" }, pk2.NomesTags);
        Assert.Equal(3, pk2.Tags.Count);
        var outro = Assert.Single(lotes, lote => lote.ClienteId == "OUTRO");
        Assert.Equal(new[] { "OUTRO.TAG1" }, outro.NomesTags);
    }

    [Fact]
    public void CriarLotes_DeduplicaSomenteAAquisicao_NaoRemoveConfiguracoes()
    {
        var entradas = new[]
        {
            new TagIaConfig { ClienteId = "PK2", TagName = "PK2.TAG27" },
            new TagIaConfig { ClienteId = "PK2", TagName = "pk2.tag27" },
        };

        var lote = Assert.Single(MonitoramentoTagsBatchPlanner.CriarLotes(entradas));

        Assert.Single(lote.NomesTags);
        Assert.Equal(2, lote.Tags.Count);
    }

    [Fact]
    public void CriarLotes_NaoAgrupaIdsDeClienteComCapitalizacaoDiferente()
    {
        var entradas = new[]
        {
            new TagIaConfig { ClienteId = "PK2", TagName = "PK2.TAG27" },
            new TagIaConfig { ClienteId = "pk2", TagName = "pk2.tag27" },
        };

        Assert.Equal(2, MonitoramentoTagsBatchPlanner.CriarLotes(entradas).Count);
    }

    [Fact]
    public void CriarLotes_AceitaNenhumaTag()
    {
        Assert.Empty(MonitoramentoTagsBatchPlanner.CriarLotes(Array.Empty<TagIaConfig>()));
    }
}
