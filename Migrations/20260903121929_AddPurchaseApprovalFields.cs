using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedByStaffId",
                table: "Purchases",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Purchases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_ApprovedByStaffId",
                table: "Purchases",
                column: "ApprovedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_AspNetUsers_ApprovedByStaffId",
                table: "Purchases",
                column: "ApprovedByStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_AspNetUsers_ApprovedByStaffId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_ApprovedByStaffId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ApprovedByStaffId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Purchases");
        }
    }
}
