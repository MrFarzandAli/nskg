using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nskg.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToVoHeadandVoDet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "VoHead",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "VoHead",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "VoHead",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "VoHead",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "VoHead",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "VoDet",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "VoDet",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "VoDet",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "VoDet",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "VoDet",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "VoHead");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "VoDet");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "VoDet");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "VoDet");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "VoDet");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "VoDet");
        }
    }
}
