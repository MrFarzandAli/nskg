using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsTovility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ISSHEAD",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "ISSHEAD",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ISSHEAD",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ISSHEAD",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "ISSHEAD",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ISSDETAIL",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "ISSDETAIL",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ISSDETAIL",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ISSDETAIL",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "ISSDETAIL",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "ISSHEAD");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ISSDETAIL");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "ISSDETAIL");
        }
    }
}
