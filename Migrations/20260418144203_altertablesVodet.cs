using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class altertablesVodet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gl3Id",
                table: "VoDet",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoDet_gl3Id",
                table: "VoDet",
                column: "gl3Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoDet_GLChart3_gl3Id",
                table: "VoDet",
                column: "gl3Id",
                principalTable: "GLChart3",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoDet_GLChart3_gl3Id",
                table: "VoDet");

            migrationBuilder.DropIndex(
                name: "IX_VoDet_gl3Id",
                table: "VoDet");

            migrationBuilder.DropColumn(
                name: "gl3Id",
                table: "VoDet");
        }
    }
}
