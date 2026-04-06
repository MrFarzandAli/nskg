using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class creataTableACTYPE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACTYPE",
                columns: table => new
                {
                    ACTYPE = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ACNAME = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    COCODE = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACTYPE", x => x.ACTYPE);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACTYPE");
        }
    }
}
