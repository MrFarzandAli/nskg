using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class creataTableAccCAT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "NSKG");

            migrationBuilder.CreateTable(
                name: "ACCCAT",
                schema: "NSKG",
                columns: table => new
                {
                    CATCODE = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CATEGORY = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    COCODE = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCCAT", x => x.CATCODE);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCCAT",
                schema: "NSKG");
        }
    }
}
