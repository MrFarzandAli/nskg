using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class alterTableACPARA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Actype",
                table: "AcPara",
                type: "nvarchar(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GLChart1Id",
                table: "AcPara",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GLChart3Id",
                table: "AcPara",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcPara_Actype",
                table: "AcPara",
                column: "Actype");

            migrationBuilder.CreateIndex(
                name: "IX_AcPara_GLChart1Id",
                table: "AcPara",
                column: "GLChart1Id");

            migrationBuilder.CreateIndex(
                name: "IX_AcPara_GLChart3Id",
                table: "AcPara",
                column: "GLChart3Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_ACTYPE_Actype",
                table: "AcPara",
                column: "Actype",
                principalTable: "ACTYPE",
                principalColumn: "ACTYPE",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_GLChart1_GLChart1Id",
                table: "AcPara",
                column: "GLChart1Id",
                principalTable: "GLChart1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_GLChart3_GLChart3Id",
                table: "AcPara",
                column: "GLChart3Id",
                principalTable: "GLChart3",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_ACTYPE_Actype",
                table: "AcPara");

            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_GLChart1_GLChart1Id",
                table: "AcPara");

            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_GLChart3_GLChart3Id",
                table: "AcPara");

            migrationBuilder.DropIndex(
                name: "IX_AcPara_Actype",
                table: "AcPara");

            migrationBuilder.DropIndex(
                name: "IX_AcPara_GLChart1Id",
                table: "AcPara");

            migrationBuilder.DropIndex(
                name: "IX_AcPara_GLChart3Id",
                table: "AcPara");

            migrationBuilder.DropColumn(
                name: "GLChart1Id",
                table: "AcPara");

            migrationBuilder.DropColumn(
                name: "GLChart3Id",
                table: "AcPara");

            migrationBuilder.AlterColumn<string>(
                name: "Actype",
                table: "AcPara",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)",
                oldNullable: true);
        }
    }
}
