using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddTableCommHeadAndCommDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                table: "RoleFormPermissions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "CommHead",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChalNo = table.Column<int>(type: "int", nullable: true),
                    Station = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Driver = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transporter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Labour = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LocalAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Advance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StationAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransporterAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdvanceAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AcCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartyExAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Lifter2Amt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherExAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotNet = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAmt1 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdvanceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AcCode1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcCode2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcType1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcType2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillTiAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DescYn = table.Column<bool>(type: "bit", nullable: false),
                    Acc1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Acc2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccName1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccName2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccAmt1 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AccAmt2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AccAcType1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccAcType2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccCType1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccCType2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescYn1 = table.Column<bool>(type: "bit", nullable: false),
                    PaidAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeliveryAmt2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommHead", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommDetail",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommHeadId = table.Column<long>(type: "bigint", nullable: false),
                    BCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cancel = table.Column<bool>(type: "bit", nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CusCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefDocNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefDocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvNo = table.Column<long>(type: "bigint", nullable: true),
                    InvDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DisAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    STaxAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    DcNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommPer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescYn = table.Column<bool>(type: "bit", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhIT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhITAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhST = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhSTAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WhITAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhSTAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhSTRec = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhITRec = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    BilNo = table.Column<int>(type: "int", nullable: true),
                    BillTiNo = table.Column<int>(type: "int", nullable: true),
                    BillTiAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaidAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ToPaidAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeliveryAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LocalAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartyExAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Lifter2Amt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherExAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FooderCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAmt2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommDetail_CommHead_CommHeadId",
                        column: x => x.CommHeadId,
                        principalTable: "CommHead",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleFormPermissions_FormId",
                table: "RoleFormPermissions",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleFormPermissions_RoleId",
                table: "RoleFormPermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CommDetail_CommHeadId",
                table: "CommDetail",
                column: "CommHeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoleFormPermissions_AspNetRoles_RoleId",
                table: "RoleFormPermissions",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleFormPermissions_Forms_FormId",
                table: "RoleFormPermissions",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoleFormPermissions_AspNetRoles_RoleId",
                table: "RoleFormPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleFormPermissions_Forms_FormId",
                table: "RoleFormPermissions");

            migrationBuilder.DropTable(
                name: "CommDetail");

            migrationBuilder.DropTable(
                name: "CommHead");

            migrationBuilder.DropIndex(
                name: "IX_RoleFormPermissions_FormId",
                table: "RoleFormPermissions");

            migrationBuilder.DropIndex(
                name: "IX_RoleFormPermissions_RoleId",
                table: "RoleFormPermissions");

            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                table: "RoleFormPermissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
