using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class addTableVourcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GLTrans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Vono = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vodate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Accode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLTrans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoHead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Vono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vodate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Votype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Totdramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Totcramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cancel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Entries = table.Column<int>(type: "int", nullable: true),
                    Cocode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Invno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Invdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ac1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hdramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Hcramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Actype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Diff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Haccode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vono2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VoucType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PersonName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Userid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpDays = table.Column<int>(type: "int", nullable: true),
                    Allow = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Totptax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Partycode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoHead", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoucherTypeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBankRequired = table.Column<bool>(type: "bit", nullable: false),
                    AllowMultiLine = table.Column<bool>(type: "bit", nullable: false),
                    AutoBalance = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherTypeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoDet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoHeadId = table.Column<int>(type: "int", nullable: false),
                    Vono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vodate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Votype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Actype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cramt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cancel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pay = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cocode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Invno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Invdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Acc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ndramt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ncramt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hacc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Chqno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Refno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Wcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Qtyin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Qtyout = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Ctype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChqDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Userid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ERate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Payment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CrDays = table.Column<int>(type: "int", nullable: true),
                    Allow = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Billtino = table.Column<int>(type: "int", nullable: true),
                    Bilno = table.Column<int>(type: "int", nullable: true),
                    Vehicleno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ptax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Transporter = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoDet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoDet_VoHead_VoHeadId",
                        column: x => x.VoHeadId,
                        principalTable: "VoHead",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoDet_VoHeadId",
                table: "VoDet",
                column: "VoHeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GLTrans");

            migrationBuilder.DropTable(
                name: "VoDet");

            migrationBuilder.DropTable(
                name: "VoucherTypeSettings");

            migrationBuilder.DropTable(
                name: "VoHead");
        }
    }
}
