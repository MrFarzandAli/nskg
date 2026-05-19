using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddchallanIdInIssueHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChallanId",
                table: "ISSHEAD",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ISSHEAD_ChallanId",
                table: "ISSHEAD",
                column: "ChallanId");

            migrationBuilder.AddForeignKey(
                name: "FK_ISSHEAD_ChallanHead_ChallanId",
                table: "ISSHEAD",
                column: "ChallanId",
                principalTable: "ChallanHead",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ISSHEAD_ChallanHead_ChallanId",
                table: "ISSHEAD");

            migrationBuilder.DropIndex(
                name: "IX_ISSHEAD_ChallanId",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "ChallanId",
                table: "ISSHEAD");
        }
    }
}
