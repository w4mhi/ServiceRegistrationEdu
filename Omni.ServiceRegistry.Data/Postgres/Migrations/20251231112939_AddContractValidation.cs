using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddContractValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastValidatedAt",
                table: "RegistrationRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationResults",
                table: "RegistrationRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidationStatus",
                table: "RegistrationRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastValidatedAt",
                table: "RegistrationRequests");

            migrationBuilder.DropColumn(
                name: "ValidationResults",
                table: "RegistrationRequests");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "RegistrationRequests");
        }
    }
}
