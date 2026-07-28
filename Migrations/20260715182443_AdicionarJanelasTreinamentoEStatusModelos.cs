using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarJanelasTreinamentoEStatusModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JanelasTreinamentoTagsIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    DataHoraInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    ModoOperacao = table.Column<string>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JanelasTreinamentoTagsIa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelosTagsIaStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    TipoModelo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Versao = table.Column<int>(type: "INTEGER", nullable: false),
                    DataUltimoTreinamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataMarcadoDesatualizado = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataInicioTreinamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataFimTreinamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimoErro = table.Column<string>(type: "TEXT", nullable: true),
                    QuantidadePontosTreinamento = table.Column<int>(type: "INTEGER", nullable: false),
                    HashJanelasTreinamento = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosTagsIaStatus", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JanelasTreinamentoTagsIa_ClienteId_TagName_DataHoraInicio_DataHoraFim",
                table: "JanelasTreinamentoTagsIa",
                columns: new[] { "ClienteId", "TagName", "DataHoraInicio", "DataHoraFim" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelosTagsIaStatus_ClienteId_TagName_TipoModelo",
                table: "ModelosTagsIaStatus",
                columns: new[] { "ClienteId", "TagName", "TipoModelo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JanelasTreinamentoTagsIa");

            migrationBuilder.DropTable(
                name: "ModelosTagsIaStatus");
        }
    }
}
