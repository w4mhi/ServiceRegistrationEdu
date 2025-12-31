using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDeletionCycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveHealthyHeartbeats",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeletionCycleCount",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ServiceDeletionCycles",
                columns: table => new
                {
                    ServiceDeletionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleNumber = table.Column<int>(type: "integer", nullable: false),
                    DeletionRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletionRequestedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeletionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DeletionApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RestorationRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestorationRequestedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RestorationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RestorationApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestorationApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RestorationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDeletionCycles", x => x.ServiceDeletionCycleId);
                    table.ForeignKey(
                        name: "FK_ServiceDeletionCycles_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDeletionCycles_DeletionApprovedAt",
                table: "ServiceDeletionCycles",
                column: "DeletionApprovedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDeletionCycles_RestorationApprovedAt",
                table: "ServiceDeletionCycles",
                column: "RestorationApprovedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDeletionCycles_ServiceId",
                table: "ServiceDeletionCycles",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDeletionCycles_ServiceId_CycleNumber",
                table: "ServiceDeletionCycles",
                columns: new[] { "ServiceId", "CycleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDeletionCycles");

            migrationBuilder.DropColumn(
                name: "ConsecutiveHealthyHeartbeats",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "DeletionCycleCount",
                table: "Services");
        }
    }
}
