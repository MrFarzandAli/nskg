using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddIdInActype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop FK first
            migrationBuilder.DropForeignKey(
                name: "FK_AcPara_ACTYPE_ACTYPE",
                table: "AcPara");

            // 2. Drop old PK
            migrationBuilder.DropPrimaryKey(
                name: "PK_ACTYPE",
                table: "ACTYPE");

            // 3. Alter column size if needed
            migrationBuilder.AlterColumn<string>(
                name: "ACTYPE",
                table: "ACTYPE",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)",
                oldMaxLength: 1);

            // 4. Add Identity Id
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ACTYPE",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "ACTYPE",
                table: "AcPara",
                type: "nvarchar(2)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)");

            // 5. Add PK on Id
            migrationBuilder.AddPrimaryKey(
                name: "PK_ACTYPE",
                table: "ACTYPE",
                column: "Id");

            // 6. Add Unique Constraint on ACTYPE
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ACTYPE_ACTYPE",
                table: "ACTYPE",
                column: "ACTYPE");

            // 7. Recreate FK
            migrationBuilder.AddForeignKey(
                name: "FK_AcPara_ACTYPE_ACTYPE",
                table: "AcPara",
                column: "ACTYPE",
                principalTable: "ACTYPE",
                principalColumn: "ACTYPE",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_ACTYPE_ACTYPE",
                table: "ACTYPE");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ACTYPE",
                table: "ACTYPE");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ACTYPE");

            migrationBuilder.AlterColumn<string>(
                name: "ACTYPE",
                table: "ACTYPE",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "ACTYPE",
                table: "AcPara",
                type: "nvarchar(1)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACTYPE",
                table: "ACTYPE",
                column: "ACTYPE");
        }
    }
}
