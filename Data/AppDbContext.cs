using Microsoft.EntityFrameworkCore;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TagIaConfig> TagsIaConfig { get; set; } = null!;
    public DbSet<TagPerfilIa> PerfisIa { get; set; } = null!;
    public DbSet<TagContextoIa> TagsContextoIa { get; set; } = null!;
    public DbSet<TagDependenciaIa> TagDependenciasIa { get; set; } = null!;
    public DbSet<AnomaliaDetectada> AnomaliasDetectadas { get; set; } = null!;
    public DbSet<AnomaliaDependenciaDetectada> AnomaliaDependenciasDetectadas { get; set; } = null!;
    public DbSet<ProcessoIa> ProcessosIa { get; set; } = null!;
    public DbSet<ProcessoTagIa> ProcessoTagsIa { get; set; } = null!;
    public DbSet<EquipamentoIa> EquipamentosIa { get; set; } = null!;
    public DbSet<EquipamentoTagIa> EquipamentoTagsIa { get; set; } = null!;
    public DbSet<ProcessoEquipamentoIa> ProcessoEquipamentosIa { get; set; } = null!;
    public DbSet<FeedbackAnomaliaIa> FeedbackAnomaliasIa { get; set; }
    public DbSet<ConfiguracaoIA> ConfiguracoesIA { get; set; }
    public DbSet<MotivoAnomaliaIA> MotivosAnomaliaIA { get; set; }
    public DbSet<AcaoRecomendadaIA> AcoesRecomendadasIA { get; set; }
    public DbSet<CategoriaFalhaIA> CategoriasFalhaIA { get; set; }
    public DbSet<JanelaTreinamentoTagIa> JanelasTreinamentoTagsIa { get; set; }
    public DbSet<ModeloTagIaStatus> ModelosTagsIaStatus { get; set; }
    public DbSet<GrupoAnaliseIa> GruposAnaliseIa { get; set; } = null!;
    public DbSet<GrupoAnaliseTagIa> GrupoAnaliseTagsIa { get; set; } = null!;
    public DbSet<RuntimeRolloutSnapshot> RuntimeRolloutSnapshots { get; set; } = null!;
    public DbSet<MultivariateIndustrialEventReviewIa> MultivariateIndustrialEventReviews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TagIaConfig>()
            .HasIndex(x => new { x.ClienteId, x.TagName })
            .IsUnique();

        modelBuilder.Entity<TagPerfilIa>()
            .HasIndex(x => new { x.ClienteId, x.TagName })
            .IsUnique();

        modelBuilder.Entity<TagContextoIa>()
            .HasIndex(x => new { x.ClienteId, x.TagName })
            .IsUnique();

        modelBuilder.Entity<TagDependenciaIa>()
            .HasIndex(x => new { x.ClienteId, x.TagName, x.TagDependente })
            .IsUnique();

        modelBuilder.Entity<AnomaliaDependenciaDetectada>()
            .HasOne(x => x.AnomaliaDetectada)
            .WithMany(x => x.Dependencias)
            .HasForeignKey(x => x.AnomaliaDetectadaId);

        modelBuilder.Entity<ProcessoIa>()
            .HasIndex(x => new { x.ClienteId, x.Nome })
            .IsUnique();

        modelBuilder.Entity<ProcessoTagIa>()
            .HasOne(x => x.ProcessoIa)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.ProcessoIaId);

        // SQLite: índice único parcial garante no máximo um processo principal
        // para cada ClienteId + TagName, sem impedir vínculos secundários.
        modelBuilder.Entity<ProcessoTagIa>()
            .HasIndex(x => new { x.ClienteId, x.TagName })
            .IsUnique()
            .HasFilter("\"Principal\" = 1");

        modelBuilder.Entity<EquipamentoIa>()
            .HasIndex(x => new { x.ClienteId, x.Nome })
            .IsUnique();

        modelBuilder.Entity<EquipamentoTagIa>()
            .HasOne(x => x.EquipamentoIa)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.EquipamentoIaId);

        modelBuilder.Entity<ProcessoEquipamentoIa>()
            .HasOne(x => x.Processo)
            .WithMany(x => x.Equipamentos)
            .HasForeignKey(x => x.ProcessoIaId);

        modelBuilder.Entity<ProcessoEquipamentoIa>()
            .HasOne(x => x.Equipamento)
            .WithMany(x => x.Processos)
            .HasForeignKey(x => x.EquipamentoIaId);

        modelBuilder.Entity<ProcessoEquipamentoIa>()
            .HasIndex(x => new
            {
                x.ProcessoIaId,
                x.EquipamentoIaId
            })
            .IsUnique();

        modelBuilder.Entity<ModeloTagIaStatus>()
            .HasIndex(x => new
            {
                x.ClienteId,
                x.TagName,
                x.TipoModelo
            })
            .IsUnique();

        modelBuilder.Entity<JanelaTreinamentoTagIa>()
            .HasIndex(x => new
            {
                x.ClienteId,
                x.TagName,
                x.DataHoraInicio,
                x.DataHoraFim
            });

        modelBuilder.Entity<GrupoAnaliseIa>()
            .HasIndex(x => new
            {
                x.ClienteId,
                x.Nome
            })
            .IsUnique();

        modelBuilder.Entity<GrupoAnaliseTagIa>()
            .HasOne(x => x.Grupo)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.GrupoAnaliseIaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GrupoAnaliseTagIa>()
            .HasIndex(x => new
            {
                x.GrupoAnaliseIaId,
                x.TagName
            })
            .IsUnique();

        modelBuilder.Entity<RuntimeRolloutSnapshot>()
            .HasIndex(x => x.GeneratedAtUtc);

        // B3.24.2 / MV08.4D1 — revisão industrial persistida.
        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .ToTable("MultivariateIndustrialEventReviews");

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.ClienteId)
            .HasMaxLength(128)
            .IsRequired();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.GroupName)
            .HasMaxLength(256)
            .IsRequired();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.SourceWindow)
            .HasMaxLength(32)
            .IsRequired();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.ReviewStatus)
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.Reviewer)
            .HasMaxLength(256)
            .IsRequired();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .Property(x => x.EvidenceReference)
            .HasMaxLength(512);

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .HasIndex(x => new
            {
                x.ClienteId,
                x.GroupName,
                x.MinuteIndex,
                x.SourceWindow,
                x.ModelVersion,
                x.ModelTrainedAtUtc,
                x.Threshold
            })
            .IsUnique();

        modelBuilder.Entity<MultivariateIndustrialEventReviewIa>()
            .HasIndex(x => new
            {
                x.ClienteId,
                x.GroupName,
                x.ReviewedAtUtc
            });
    }
}