using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class RC5_AddTagRegimeComponente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapaEstados",
                table: "TagsRegimesOperacionaisIa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TagsRegimesComponentesIa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfiguracaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false),
                    TagRegime = table.Column<string>(type: "TEXT", nullable: false),
                    MapaEstados = table.Column<string>(type: "TEXT", nullable: true),
                    ToleranciaEstado = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagsRegimesComponentesIa", x => x.Id);
                    table.CheckConstraint("CK_TagsRegimesComponentesIa_Ordem", "\"Ordem\" >= 2");
                    table.ForeignKey(
                        name: "FK_TagsRegimesComponentesIa_TagsRegimesOperacionaisIa_ConfiguracaoId",
                        column: x => x.ConfiguracaoId,
                        principalTable: "TagsRegimesOperacionaisIa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagsRegimesComponentesIa_ConfiguracaoId_Ordem",
                table: "TagsRegimesComponentesIa",
                columns: new[] { "ConfiguracaoId", "Ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagsRegimesComponentesIa");

            migrationBuilder.DropColumn(
                name: "MapaEstados",
                table: "TagsRegimesOperacionaisIa");
        }
    }
}
