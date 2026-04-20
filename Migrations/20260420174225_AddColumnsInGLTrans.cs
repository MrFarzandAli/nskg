using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnsInGLTrans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.RenameColumn(
                name: "Vodate",
                table: "GLTrans",
                newName: "Docdate");

            migrationBuilder.RenameColumn(
                name: "Vono",
                table: "GLTrans",
                newName: "DocNo");

            migrationBuilder.AddColumn<string>(
                name: "ContraAcc",
                table: "GLTrans",
                type: "nvarchar(max)",
                nullable: true);


            migrationBuilder.AddColumn<int>(
                name: "LineNo",
                table: "GLTrans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RefId",
                table: "GLTrans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefType",
                table: "GLTrans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Votype",
                table: "GLTrans",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContraAcc",
                table: "GLTrans");


            migrationBuilder.DropColumn(
                name: "LineNo",
                table: "GLTrans");

            migrationBuilder.DropColumn(
                name: "RefId",
                table: "GLTrans");

            migrationBuilder.DropColumn(
                name: "RefType",
                table: "GLTrans");

            migrationBuilder.DropColumn(
                name: "Votype",
                table: "GLTrans");

            migrationBuilder.RenameColumn(
                name: "Docdate",
                table: "GLTrans",
                newName: "Vodate");

            migrationBuilder.RenameColumn(
                name: "DocNo",
                table: "GLTrans",
                newName: "Vono");

         
        }
    }
}
