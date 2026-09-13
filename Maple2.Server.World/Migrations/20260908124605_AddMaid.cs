using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class AddMaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Item",
                table: "character-shop-item-data",
                type: "json",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "json")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "maid",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CubeUid = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    HireTime = table.Column<long>(type: "bigint", nullable: false),
                    ExpiryTime = table.Column<long>(type: "bigint", nullable: false),
                    PayTime = table.Column<long>(type: "bigint", nullable: false),
                    ClosenessLevel = table.Column<int>(type: "int", nullable: false),
                    ClosenessExp = table.Column<int>(type: "int", nullable: false),
                    ClosenessTime = table.Column<long>(type: "bigint", nullable: false),
                    Mood = table.Column<int>(type: "int", nullable: false),
                    MoodTime = table.Column<long>(type: "bigint", nullable: false),
                    CraftRecipeId = table.Column<int>(type: "int", nullable: false),
                    CraftStartTime = table.Column<long>(type: "bigint", nullable: false),
                    CraftLeadTime = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maid", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maid");

            migrationBuilder.UpdateData(
                table: "character-shop-item-data",
                keyColumn: "Item",
                keyValue: null,
                column: "Item",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Item",
                table: "character-shop-item-data",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
