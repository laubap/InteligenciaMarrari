using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class CriarCentralIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesIA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    HorasHistorico = table.Column<int>(type: "INTEGER", nullable: false),
                    Sensibilidade = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfiancaMinima = table.Column<int>(type: "INTEGER", nullable: false),
                    AprendizadoAtivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificacaoPopup = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificacaoEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesIA", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivosAnomaliaIA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosAnomaliaIA", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcoesRecomendadasIA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MotivoAnomaliaIAId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcoesRecomendadasIA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoesRecomendadasIA_MotivosAnomaliaIA_MotivoAnomaliaIAId",
                        column: x => x.MotivoAnomaliaIAId,
                        principalTable: "MotivosAnomaliaIA",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesRecomendadasIA_MotivoAnomaliaIAId",
                table: "AcoesRecomendadasIA",
                column: "MotivoAnomaliaIAId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoesRecomendadasIA");

            migrationBuilder.DropTable(
                name: "ConfiguracoesIA");

            migrationBuilder.DropTable(
                name: "MotivosAnomaliaIA");
        }
    }
}
