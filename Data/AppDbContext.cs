using Microsoft.EntityFrameworkCore;

// Contexto do Entity Framework para a aplicação.
// Define os DbSets para todas as tabelas/entidades usadas pela API.
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    base.OnModelCreating(modelBuilder);

    // Índices únicos para evitar registros duplicados por cliente/tag.
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

    // Relações entre entidades para garantir integridade referencial.
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

}
}