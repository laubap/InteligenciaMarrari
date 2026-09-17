using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTagRegimeOperacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TagsRegimesOperacionaisIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    TagRegime = table.Column<string>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    LagMinutos = table.Column<int>(type: "INTEGER", nullable: false),
                    ToleranciaAlinhamentoMinutos = table.Column<int>(type: "INTEGER", nullable: false),
                    ToleranciaEstado = table.Column<double>(type: "REAL", nullable: false),
                    MinimoAmostrasPorRegime = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagsRegimesOperacionaisIa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagsRegimesOperacionaisIa_ClienteId_TagName",
                table: "TagsRegimesOperacionaisIa",
                columns: new[] { "ClienteId", "TagName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagsRegimesOperacionaisIa");
        }
    }
}
