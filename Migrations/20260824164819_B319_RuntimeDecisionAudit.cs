using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class B319_RuntimeDecisionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmitNewEvidence",
                table: "AnomaliasDetectadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegacyEhAnomalia",
                table: "AnomaliasDetectadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RuntimeCandidateBaselineReady",
                table: "AnomaliasDetectadas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeDecisionReasonsJson",
                table: "AnomaliasDetectadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeDecisionSource",
                table: "AnomaliasDetectadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeIncidentId",
                table: "AnomaliasDetectadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeRegimeState",
                table: "AnomaliasDetectadas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RuntimeUsedLegacyFallback",
                table: "AnomaliasDetectadas",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmitNewEvidence",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "LegacyEhAnomalia",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeCandidateBaselineReady",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeDecisionReasonsJson",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeDecisionSource",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeIncidentId",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeRegimeState",
                table: "AnomaliasDetectadas");

            migrationBuilder.DropColumn(
                name: "RuntimeUsedLegacyFallback",
                table: "AnomaliasDetectadas");
        }
    }
}
