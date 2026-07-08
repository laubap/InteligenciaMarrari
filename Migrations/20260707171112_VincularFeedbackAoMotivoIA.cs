using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class VincularFeedbackAoMotivoIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.AddColumn<int>(
                name: "MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackAnomaliasIa_MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa",
                column: "MotivoAnomaliaIAId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackAnomaliasIa_MotivosAnomaliaIA_MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa",
                column: "MotivoAnomaliaIAId",
                principalTable: "MotivosAnomaliaIA",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackAnomaliasIa_MotivosAnomaliaIA_MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.DropIndex(
                name: "IX_FeedbackAnomaliasIa_MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.DropColumn(
                name: "MotivoAnomaliaIAId",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "FeedbackAnomaliasIa",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
