using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class alterTableVoHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead");

            migrationBuilder.AlterColumn<int>(
                name: "gl3Id",
                table: "VoHead",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead",
                column: "gl3Id",
                principalTable: "GLChart3",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead");

            migrationBuilder.AlterColumn<int>(
                name: "gl3Id",
                table: "VoHead",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead",
                column: "gl3Id",
                principalTable: "GLChart3",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
