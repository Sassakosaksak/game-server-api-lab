using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRankingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Players_Gold_Level_Id",
                table: "Players",
                columns: new[] { "Gold", "Level", "Id" },
                descending: new[] { true, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_Gold_Level_Id",
                table: "Players");
        }
    }
}
