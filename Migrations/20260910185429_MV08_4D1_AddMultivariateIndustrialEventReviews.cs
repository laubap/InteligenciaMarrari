using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class MV08_4D1_AddMultivariateIndustrialEventReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MultivariateIndustrialEventReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MinuteIndex = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceWindow = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ModelVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelTrainedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Threshold = table.Column<double>(type: "REAL", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Reviewer = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceReference = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultivariateIndustrialEventReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MultivariateIndustrialEventReviews_ClienteId_GroupName_MinuteIndex_SourceWindow_ModelVersion_ModelTrainedAtUtc_Threshold",
                table: "MultivariateIndustrialEventReviews",
                columns: new[] { "ClienteId", "GroupName", "MinuteIndex", "SourceWindow", "ModelVersion", "ModelTrainedAtUtc", "Threshold" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MultivariateIndustrialEventReviews_ClienteId_GroupName_ReviewedAtUtc",
                table: "MultivariateIndustrialEventReviews",
                columns: new[] { "ClienteId", "GroupName", "ReviewedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MultivariateIndustrialEventReviews");
        }
    }
}
