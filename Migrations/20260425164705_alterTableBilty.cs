using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class alterTableBilty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BilNo",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillTiNo",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartType",
                table: "ISSHEAD",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartType2",
                table: "ISSHEAD",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartType3",
                table: "ISSHEAD",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cartage1",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cartage2",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cartage3",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommPer",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DCNO",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescYN",
                table: "ISSHEAD",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescYN1",
                table: "ISSHEAD",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "ISSHEAD",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fooder",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FooderCode",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IName",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Labour",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Lifter2",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NTN",
                table: "ISSHEAD",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherEx",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PType",
                table: "ISSHEAD",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartyEx",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "STaxNo",
                table: "ISSHEAD",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendTo",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "T_T",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrName",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrName2",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrName3",
                table: "ISSHEAD",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transporter",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transporter2",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transporter3",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleNo",
                table: "ISSHEAD",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WH_IT",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WH_IT_Acc",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WH_IT_Amt",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WH_IT_Rec",
                table: "ISSHEAD",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WH_ST",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WH_ST_Acc",
                table: "ISSHEAD",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WH_ST_Amt",
                table: "ISSHEAD",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WH_ST_Rec",
                table: "ISSHEAD",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount1",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BilNo",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillTiNo",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Comm",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Comm2",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Comm3",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CusName",
                table: "ISSDETAIL",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescCode",
                table: "ISSDETAIL",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fooder",
                table: "ISSDETAIL",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormulaCode",
                table: "ISSDETAIL",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Freight",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvDate",
                table: "ISSDETAIL",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvNo",
                table: "ISSDETAIL",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PCommPer",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackingN",
                table: "ISSDETAIL",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotAmt",
                table: "ISSDETAIL",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleNo",
                table: "ISSDETAIL",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BilNo",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "BillTiNo",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CartType",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CartType2",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CartType3",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Cartage1",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Cartage2",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Cartage3",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CommPer",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "DCNO",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "DescYN",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "DescYN1",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Fooder",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "FooderCode",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "IName",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Labour",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Lifter2",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "NTN",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "OtherEx",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "PType",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "PartyEx",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "STaxNo",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "SendTo",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "T_T",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "TrName",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "TrName2",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "TrName3",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Transporter",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Transporter2",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Transporter3",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "VehicleNo",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_IT",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_IT_Acc",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_IT_Amt",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_IT_Rec",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_ST",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_ST_Acc",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_ST_Amt",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "WH_ST_Rec",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "Amount1",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "BilNo",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "BillTiNo",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Comm",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Comm2",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Comm3",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "CusName",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "DescCode",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Fooder",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "FormulaCode",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "Freight",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "InvDate",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "InvNo",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "PCommPer",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "PackingN",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "TotAmt",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "VehicleNo",
                table: "ISSDETAIL");
        }
    }
}
