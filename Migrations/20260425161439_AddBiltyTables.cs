using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddBiltyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ISSHEAD",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FyId = table.Column<int>(type: "int", nullable: false),
                    BCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Cancel = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DocNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CusCode = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    RefDocNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    RefDocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvNo = table.Column<long>(type: "bigint", nullable: true),
                    InvDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DisAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STaxAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AccCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    AcType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    CusName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CusAdd = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CusTel = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ISSHEAD", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ISSDETAIL",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FyId = table.Column<int>(type: "int", nullable: false),
                    IssHeadId = table.Column<int>(type: "int", nullable: false),
                    BCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    WCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DocNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CusCode = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    IType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AccCode = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    RefDocNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    RefDocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Packs = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QtyPerPack = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    STax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STaxAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AmtNet = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ISSDETAIL", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ISSDETAIL_ISSHEAD_IssHeadId",
                        column: x => x.IssHeadId,
                        principalTable: "ISSHEAD",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ISSDETAIL_IssHeadId",
                table: "ISSDETAIL",
                column: "IssHeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ISSDETAIL");

            migrationBuilder.DropTable(
                name: "ISSHEAD");
        }
    }
}
