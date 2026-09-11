using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAApi.Migrations
{
    /// <inheritdoc />
    public partial class B32013_ProcessoPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Principal",
                table: "ProcessoTagsIa",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessoTagsIa_ClienteId_TagName",
                table: "ProcessoTagsIa",
                columns: new[] { "ClienteId", "TagName" },
                unique: true,
                filter: "\"Principal\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessoTagsIa_ClienteId_TagName",
                table: "ProcessoTagsIa");

            migrationBuilder.DropColumn(
                name: "Principal",
                table: "ProcessoTagsIa");
        }
    }
}
