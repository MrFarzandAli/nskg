using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AlterTableIssHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "ISSHEAD",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StationId",
                table: "ISSHEAD",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ISSHEAD_CustomerId",
                table: "ISSHEAD",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ISSHEAD_StationId",
                table: "ISSHEAD",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ISSHEAD_GLChart3_CustomerId",
                table: "ISSHEAD",
                column: "CustomerId",
                principalTable: "GLChart3",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ISSHEAD_GLChart3_StationId",
                table: "ISSHEAD",
                column: "StationId",
                principalTable: "GLChart3",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ISSHEAD_GLChart3_CustomerId",
                table: "ISSHEAD");

            migrationBuilder.DropForeignKey(
                name: "FK_ISSHEAD_GLChart3_StationId",
                table: "ISSHEAD");

            migrationBuilder.DropIndex(
                name: "IX_ISSHEAD_CustomerId",
                table: "ISSHEAD");

            migrationBuilder.DropIndex(
                name: "IX_ISSHEAD_StationId",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "ISSHEAD");
        }
    }
}
