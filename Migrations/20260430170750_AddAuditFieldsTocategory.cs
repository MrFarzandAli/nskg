using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsTocategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                schema: "NSKG",
                table: "ACCCAT",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "NSKG",
                table: "ACCCAT",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                schema: "NSKG",
                table: "ACCCAT",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                schema: "NSKG",
                table: "ACCCAT",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "NSKG",
                table: "ACCCAT");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                schema: "NSKG",
                table: "ACCCAT");
        }
    }
}
