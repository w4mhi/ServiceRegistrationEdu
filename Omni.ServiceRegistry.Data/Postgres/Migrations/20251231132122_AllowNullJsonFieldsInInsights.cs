using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullJsonFieldsInInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RootCauses",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 5000);

            migrationBuilder.AlterColumn<string>(
                name: "RecommendedActions",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 3000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 3000);

            migrationBuilder.AlterColumn<string>(
                name: "CorrelatedServices",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 3000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 3000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RootCauses",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 5000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 5000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecommendedActions",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 3000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 3000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CorrelatedServices",
                table: "ServiceHealthInsights",
                type: "jsonb",
                maxLength: 3000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldMaxLength: 3000,
                oldNullable: true);
        }
    }
}
