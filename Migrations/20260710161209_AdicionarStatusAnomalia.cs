using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarStatusAnomalia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AnomaliasDetectadas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "AnomaliasDetectadas");
        }
    }
}
