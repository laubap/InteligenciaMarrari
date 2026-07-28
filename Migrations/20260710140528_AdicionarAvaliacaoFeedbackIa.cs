using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAvaliacaoFeedbackIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvaliacaoAcao",
                table: "FeedbackAnomaliasIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvaliacaoCausa",
                table: "FeedbackAnomaliasIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvaliacaoDeteccao",
                table: "FeedbackAnomaliasIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvaliacaoAcao",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.DropColumn(
                name: "AvaliacaoCausa",
                table: "FeedbackAnomaliasIa");

            migrationBuilder.DropColumn(
                name: "AvaliacaoDeteccao",
                table: "FeedbackAnomaliasIa");
        }
    }
}
