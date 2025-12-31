using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddApiEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiEndpoints",
                table: "Services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiEndpoints",
                table: "RegistrationRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiEndpoints",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ApiEndpoints",
                table: "RegistrationRequests");
        }
    }
}
