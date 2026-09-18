using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class B325_AddBehavioralMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TagsComportamentosIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Origem = table.Column<string>(type: "TEXT", nullable: false),
                    Classificacao = table.Column<string>(type: "TEXT", nullable: false),
                    PrimeiraObservacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaObservacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QuantidadeOcorrencias = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmostras = table.Column<long>(type: "INTEGER", nullable: false),
                    Confianca = table.Column<double>(type: "REAL", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagsComportamentosIa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagsComportamentosOcorrenciasIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagComportamentoIaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataHoraInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QuantidadeAmostras = table.Column<int>(type: "INTEGER", nullable: false),
                    Confianca = table.Column<double>(type: "REAL", nullable: false),
                    UtilizadaNoAprendizado = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagsComportamentosOcorrenciasIa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagsComportamentosOcorrenciasIa_TagsComportamentosIa_TagComportamentoIaId",
                        column: x => x.TagComportamentoIaId,
                        principalTable: "TagsComportamentosIa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagsComportamentosSnapshotsIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagComportamentoIaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Versao = table.Column<int>(type: "INTEGER", nullable: false),
                    Media = table.Column<double>(type: "REAL", nullable: false),
                    DesvioPadrao = table.Column<double>(type: "REAL", nullable: false),
                    Minimo = table.Column<double>(type: "REAL", nullable: false),
                    Maximo = table.Column<double>(type: "REAL", nullable: false),
                    Amplitude = table.Column<double>(type: "REAL", nullable: false),
                    PercentualZeros = table.Column<double>(type: "REAL", nullable: false),
                    VariacaoMedia = table.Column<double>(type: "REAL", nullable: false),
                    QuantidadePicos = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRegistrosHistorico = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRegistrosUsados = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalOutliersRemovidos = table.Column<int>(type: "INTEGER", nullable: false),
                    DeadbandRecomendado = table.Column<double>(type: "REAL", nullable: false),
                    ConfiancaDeadband = table.Column<double>(type: "REAL", nullable: false),
                    JustificativaDeadband = table.Column<string>(type: "TEXT", nullable: true),
                    PeriodoInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodoFim = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagsComportamentosSnapshotsIa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagsComportamentosSnapshotsIa_TagsComportamentosIa_TagComportamentoIaId",
                        column: x => x.TagComportamentoIaId,
                        principalTable: "TagsComportamentosIa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosIa_ClienteId_TagName_Codigo",
                table: "TagsComportamentosIa",
                columns: new[] { "ClienteId", "TagName", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosIa_ClienteId_TagName_Status",
                table: "TagsComportamentosIa",
                columns: new[] { "ClienteId", "TagName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosOcorrenciasIa_TagComportamentoIaId_DataHoraInicio",
                table: "TagsComportamentosOcorrenciasIa",
                columns: new[] { "TagComportamentoIaId", "DataHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosOcorrenciasIa_TagComportamentoIaId_DataHoraInicio_DataHoraFim",
                table: "TagsComportamentosOcorrenciasIa",
                columns: new[] { "TagComportamentoIaId", "DataHoraInicio", "DataHoraFim" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosSnapshotsIa_TagComportamentoIaId_DataCriacao",
                table: "TagsComportamentosSnapshotsIa",
                columns: new[] { "TagComportamentoIaId", "DataCriacao" });

            migrationBuilder.CreateIndex(
                name: "IX_TagsComportamentosSnapshotsIa_TagComportamentoIaId_Versao",
                table: "TagsComportamentosSnapshotsIa",
                columns: new[] { "TagComportamentoIaId", "Versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagsComportamentosOcorrenciasIa");

            migrationBuilder.DropTable(
                name: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropTable(
                name: "TagsComportamentosIa");
        }
    }
}
