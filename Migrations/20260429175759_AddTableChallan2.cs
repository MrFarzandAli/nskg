using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddTableChallan2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChallanHead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FyId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChalNo = table.Column<int>(type: "int", nullable: true),
                    Station = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Driver = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transporter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotBillTi = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LocalAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DescYn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotToPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotPartyEx = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotLifter2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotOtherEx = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillTiAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeliveryAmt2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherEx2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocalAmt2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartyEx2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PExpCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PExpCode2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PExpCode3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PExpAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PExpAmt2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PExpAmt3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PExpBilti = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PExpBilti2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PExpBilti3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartyStationCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallanHead", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChallanDet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FyId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ChallanHeadId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cnacel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CusCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefDocNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefDocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvNo = table.Column<long>(type: "bigint", nullable: true),
                    InvDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DisAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STaxAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CusName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CusAdd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CusTel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Qty = table.Column<int>(type: "int", nullable: true),
                    STaxNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NTN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transporter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DCNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommPer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescYn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhIt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhItAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhSt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhStAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhItAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhStAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhStRec = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhItRec = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transporter2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transporter3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrName2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrName3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cartage1 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cartage2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cartage3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CartType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CartType2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CartType3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fooder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SendTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Labour = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BilNo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BillTiNo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BillTiAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaidAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ToPaidAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartyEx = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Lifter2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherEx = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BillTiDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallanDet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallanDet_ChallanHead_ChallanHeadId",
                        column: x => x.ChallanHeadId,
                        principalTable: "ChallanHead",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallanDet_ChallanHeadId",
                table: "ChallanDet",
                column: "ChallanHeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChallanDet");

            migrationBuilder.DropTable(
                name: "ChallanHead");
        }
    }
}
