using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class B3208_RuntimeRolloutSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeRolloutSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RuntimeDecisionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanaryOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedClientIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ReadyToExpand = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalDecisions = table.Column<long>(type: "INTEGER", nullable: false),
                    ObservationMinutes = table.Column<double>(type: "REAL", nullable: false),
                    LegacyFallbackRate = table.Column<double>(type: "REAL", nullable: false),
                    DataQualityUncertainRate = table.Column<double>(type: "REAL", nullable: false),
                    CandidateMismatchRate = table.Column<double>(type: "REAL", nullable: false),
                    StableUnresolvedContinuationRate = table.Column<double>(type: "REAL", nullable: false),
                    ReasonsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FirstDecisionAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastDecisionAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeRolloutSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeRolloutSnapshots_GeneratedAtUtc",
                table: "RuntimeRolloutSnapshots",
                column: "GeneratedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeRolloutSnapshots");
        }
    }
}
