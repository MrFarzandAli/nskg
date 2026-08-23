using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddIdInAccCat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ACCCAT",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.AlterColumn<string>(
                name: "COCODE",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "CATCODE",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "NSKG",
                table: "ACCCAT",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACCCAT",
                schema: "NSKG",
                table: "ACCCAT",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ACCCAT",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.AlterColumn<string>(
                name: "COCODE",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CATCODE",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACCCAT",
                schema: "NSKG",
                table: "ACCCAT",
                column: "CATCODE");
        }
    }
}
