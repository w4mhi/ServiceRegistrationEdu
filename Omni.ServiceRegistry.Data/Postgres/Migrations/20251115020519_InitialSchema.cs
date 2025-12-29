using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
#pragma warning disable CA1062 // Auto-generated migration code
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationRequests",
                columns: table => new
                {
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ServiceNameNormalized = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Endpoints = table.Column<string>(type: "jsonb", nullable: false),
                    HeartbeatTimeout = table.Column<int>(type: "integer", nullable: false),
                    MaxMissedHeartbeats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewComments = table.Column<string>(type: "text", nullable: true),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationRequests", x => x.RegistrationId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceChangeHistory",
                columns: table => new
                {
                    ChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangeDescription = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceChangeHistory", x => x.ChangeId);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ServiceNameNormalized = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Endpoints = table.Column<string>(type: "jsonb", nullable: false),
                    HeartbeatTimeout = table.Column<int>(type: "integer", nullable: false),
                    MaxMissedHeartbeats = table.Column<int>(type: "integer", nullable: false),
                    HealthStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MissedHeartbeatCounter = table.Column<int>(type: "integer", nullable: false),
                    HeartbeatCount = table.Column<long>(type: "bigint", nullable: false),
                    DeletionStatus = table.Column<int>(type: "integer", nullable: false),
                    DeletionRequestedBy = table.Column<string>(type: "text", nullable: true),
                    DeletionRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.ServiceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRequests_ServiceNameNormalized",
                table: "RegistrationRequests",
                column: "ServiceNameNormalized",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRequests_Status",
                table: "RegistrationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeHistory_ChangedAt",
                table: "ServiceChangeHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeHistory_ServiceId",
                table: "ServiceChangeHistory",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ContactEmail",
                table: "Services",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Services_HealthStatus",
                table: "Services",
                column: "HealthStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Services_LastHeartbeatTimestamp",
                table: "Services",
                column: "LastHeartbeatTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ServiceNameNormalized",
                table: "Services",
                column: "ServiceNameNormalized",
                unique: true,
                filter: "\"DeletionStatus\" != 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
#pragma warning restore CA1062
                name: "RegistrationRequests");

            migrationBuilder.DropTable(
                name: "ServiceChangeHistory");

            migrationBuilder.DropTable(
                name: "Services");
        }
    }
}
