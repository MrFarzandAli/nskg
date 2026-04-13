using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class createChartOfAccountTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GLChart1",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AC1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncBal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Opening = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CoCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PLSQ = table.Column<int>(type: "int", nullable: true),
                    BSSQ = table.Column<int>(type: "int", nullable: true),
                    ACCCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrevBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubCatCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLChart1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GLChart3",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GLChart1Id = table.Column<int>(type: "int", nullable: false),
                    AC1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AC2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AC3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncBal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Opening = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Plot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STaxNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Add1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Add2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Add3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Closing = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NTN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rate2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SPer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ACC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrevBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CHName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CRDays = table.Column<int>(type: "int", nullable: true),
                    CommPer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CommExp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommExp2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommExp3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WH_IT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WH_ST = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WH_IT_ACC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WH_ST_ACC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPComm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OPComm2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OPComm3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TR_DRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TR_LRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TR_RRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Bharti = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrBill = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrRec = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLChart3", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GLChart3_GLChart1_GLChart1Id",
                        column: x => x.GLChart1Id,
                        principalTable: "GLChart1",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GLChart3_GLChart1Id",
                table: "GLChart3",
                column: "GLChart1Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GLChart3");

            migrationBuilder.DropTable(
                name: "GLChart1");
        }
    }
}
