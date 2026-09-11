using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadbandFieldsToTagProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAprovacaoVariacaoMinima",
                table: "TagsContextoIa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaVariacaoMinima",
                table: "TagsContextoIa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VariacaoMinimaAprovadaPeloCliente",
                table: "TagsContextoIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "VariacaoMinimaRelevanteCliente",
                table: "TagsContextoIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ConfiancaDeadband",
                table: "PerfisIa",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DeadbandSugerido",
                table: "PerfisIa",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaDeadbandSugerido",
                table: "PerfisIa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VariacaoMinimaCalculadaAutomaticamente",
                table: "PerfisIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "VariacaoMinimaRelevante",
                table: "PerfisIa",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataAprovacaoVariacaoMinima",
                table: "TagsContextoIa");

            migrationBuilder.DropColumn(
                name: "JustificativaVariacaoMinima",
                table: "TagsContextoIa");

            migrationBuilder.DropColumn(
                name: "VariacaoMinimaAprovadaPeloCliente",
                table: "TagsContextoIa");

            migrationBuilder.DropColumn(
                name: "VariacaoMinimaRelevanteCliente",
                table: "TagsContextoIa");

            migrationBuilder.DropColumn(
                name: "ConfiancaDeadband",
                table: "PerfisIa");

            migrationBuilder.DropColumn(
                name: "DeadbandSugerido",
                table: "PerfisIa");

            migrationBuilder.DropColumn(
                name: "JustificativaDeadbandSugerido",
                table: "PerfisIa");

            migrationBuilder.DropColumn(
                name: "VariacaoMinimaCalculadaAutomaticamente",
                table: "PerfisIa");

            migrationBuilder.DropColumn(
                name: "VariacaoMinimaRelevante",
                table: "PerfisIa");
        }
    }
}
