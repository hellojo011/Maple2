using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class MaidKeyedByAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_maid_AccountId",
                table: "maid");

            migrationBuilder.DropIndex(
                name: "IX_maid_CubeUid",
                table: "maid");

            migrationBuilder.AddColumn<int>(
                name: "MaidId",
                table: "maid",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_maid_AccountId_MaidId",
                table: "maid",
                columns: new[] { "AccountId", "MaidId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_maid_AccountId_MaidId",
                table: "maid");

            migrationBuilder.DropColumn(
                name: "MaidId",
                table: "maid");

            migrationBuilder.CreateIndex(
                name: "IX_maid_AccountId",
                table: "maid",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_maid_CubeUid",
                table: "maid",
                column: "CubeUid",
                unique: true);
        }
    }
}
