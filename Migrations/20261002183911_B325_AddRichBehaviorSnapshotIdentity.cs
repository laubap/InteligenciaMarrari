using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class B325_AddRichBehaviorSnapshotIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "IntervaloInterquartil",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Mediana",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PrimeiroQuartil",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RobustScale",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TerceiroQuartil",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TypicalAbsoluteStep",
                table: "TagsComportamentosSnapshotsIa",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervaloInterquartil",
                table: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropColumn(
                name: "Mediana",
                table: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropColumn(
                name: "PrimeiroQuartil",
                table: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropColumn(
                name: "RobustScale",
                table: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropColumn(
                name: "TerceiroQuartil",
                table: "TagsComportamentosSnapshotsIa");

            migrationBuilder.DropColumn(
                name: "TypicalAbsoluteStep",
                table: "TagsComportamentosSnapshotsIa");
        }
    }
}
