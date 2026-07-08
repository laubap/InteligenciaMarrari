using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class CriarCategoriasFalhaIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriasFalhaIA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasFalhaIA", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MotivosAnomaliaIA_CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA",
                column: "CategoriaFalhaIAId");

            migrationBuilder.AddForeignKey(
                name: "FK_MotivosAnomaliaIA_CategoriasFalhaIA_CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA",
                column: "CategoriaFalhaIAId",
                principalTable: "CategoriasFalhaIA",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MotivosAnomaliaIA_CategoriasFalhaIA_CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA");

            migrationBuilder.DropTable(
                name: "CategoriasFalhaIA");

            migrationBuilder.DropIndex(
                name: "IX_MotivosAnomaliaIA_CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA");

            migrationBuilder.DropColumn(
                name: "CategoriaFalhaIAId",
                table: "MotivosAnomaliaIA");
        }
    }
}
