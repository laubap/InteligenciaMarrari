using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoAnaliseIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GruposAnaliseIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MinimumAvailableTags = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxLagMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    PesoEnsemble = table.Column<double>(type: "REAL", nullable: false),
                    Sensibilidade = table.Column<double>(type: "REAL", nullable: false),
                    Algoritmo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposAnaliseIa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GrupoAnaliseTagsIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrupoAnaliseIaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoAnaliseTagsIa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrupoAnaliseTagsIa_GruposAnaliseIa_GrupoAnaliseIaId",
                        column: x => x.GrupoAnaliseIaId,
                        principalTable: "GruposAnaliseIa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrupoAnaliseTagsIa_GrupoAnaliseIaId_TagName",
                table: "GrupoAnaliseTagsIa",
                columns: new[] { "GrupoAnaliseIaId", "TagName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposAnaliseIa_ClienteId_Nome",
                table: "GruposAnaliseIa",
                columns: new[] { "ClienteId", "Nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrupoAnaliseTagsIa");

            migrationBuilder.DropTable(
                name: "GruposAnaliseIa");
        }
    }
}
