using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDeletionFields : Migration
    {
        /// <inheritdoc />
#pragma warning disable CA1062 // Auto-generated migration code
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionApprovedAt",
                table: "Services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionApprovedBy",
                table: "Services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionComments",
                table: "Services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "Services",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
#pragma warning restore CA1062
                name: "DeletionApprovedAt",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "DeletionApprovedBy",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "DeletionComments",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "Services");
        }
    }
}
