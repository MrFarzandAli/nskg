using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class fix_actype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_ACTYPE_Actype",
                table: "AcPara");


            migrationBuilder.RenameColumn(
                name: "Actype",
                table: "AcPara",
                newName: "ACTYPE");



            migrationBuilder.RenameIndex(
                name: "IX_AcPara_Actype",
                table: "AcPara",
                newName: "IX_AcPara_ACTYPE");


            migrationBuilder.AlterColumn<string>(
                name: "ACTYPE",
                table: "AcPara",
                type: "nvarchar(1)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_ACTYPE_ACTYPE",
                table: "AcPara",
                column: "ACTYPE",
                principalTable: "ACTYPE",
                principalColumn: "ACTYPE",
                onDelete: ReferentialAction.Restrict);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_ACTYPE_ACTYPE",
                table: "AcPara");



            migrationBuilder.RenameColumn(
                name: "ACTYPE",
                table: "AcPara",
                newName: "Actype");



            migrationBuilder.RenameIndex(
                name: "IX_AcPara_ACTYPE",
                table: "AcPara",
                newName: "IX_AcPara_Actype");

            migrationBuilder.AlterColumn<string>(
                name: "Actype",
                table: "AcPara",
                type: "nvarchar(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)");


            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_ACTYPE_ACTYPE",
                table: "AcPara",
                column: "ACTYPE",
                principalTable: "ACTYPE",
                principalColumn: "ACTYPE");



        }
    }
}
