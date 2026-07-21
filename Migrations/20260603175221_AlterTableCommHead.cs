using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AlterTableCommHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StationId",
                table: "CommHead",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransId",
                table: "CommHead",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StationId",
                table: "CommHead");

            migrationBuilder.DropColumn(
                name: "TransId",
                table: "CommHead");
        }
    }
}
