using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class altertablesCompanyAndVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gl3Id",
                table: "VoHead",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Cocode",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

        
            

            migrationBuilder.CreateIndex(
                name: "IX_VoHead_gl3Id",
                table: "VoHead",
                column: "gl3Id");

         
           
            migrationBuilder.AddForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead",
                column: "gl3Id",
                principalTable: "GLChart3",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.DropForeignKey(
                name: "FK_VoHead_GLChart3_gl3Id",
                table: "VoHead");

            migrationBuilder.DropIndex(
                name: "IX_VoHead_gl3Id",
                table: "VoHead");

           
            migrationBuilder.DropColumn(
                name: "gl3Id",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "Cocode",
                table: "Companies");

           
        }
    }
}
