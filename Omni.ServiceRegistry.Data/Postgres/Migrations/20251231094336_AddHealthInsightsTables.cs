using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.ServiceRegistry.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthInsightsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisTriggerLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceIds = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisTriggerLogs", x => x.LogId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceHealthInsights",
                columns: table => new
                {
                    InsightId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RootCauses = table.Column<string>(type: "jsonb", maxLength: 5000, nullable: false),
                    CorrelatedServices = table.Column<string>(type: "jsonb", maxLength: 3000, nullable: false),
                    HistoricalContext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecommendedActions = table.Column<string>(type: "jsonb", maxLength: 3000, nullable: false),
                    LlmModel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    ProcessingTimeMs = table.Column<int>(type: "integer", nullable: false),
                    AnalysisStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContextData = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceHealthInsights", x => x.InsightId);
                    table.ForeignKey(
                        name: "FK_ServiceHealthInsights_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisTriggerLogs_TriggeredAt",
                table: "AnalysisTriggerLogs",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisTriggerLogs_TriggeredBy",
                table: "AnalysisTriggerLogs",
                column: "TriggeredBy");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisTriggerLogs_TriggeredBy_TriggeredAt",
                table: "AnalysisTriggerLogs",
                columns: new[] { "TriggeredBy", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHealthInsights_AnalysisStatus",
                table: "ServiceHealthInsights",
                column: "AnalysisStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHealthInsights_GeneratedAt",
                table: "ServiceHealthInsights",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHealthInsights_ServiceId",
                table: "ServiceHealthInsights",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHealthInsights_ServiceId_GeneratedAt",
                table: "ServiceHealthInsights",
                columns: new[] { "ServiceId", "GeneratedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisTriggerLogs");

            migrationBuilder.DropTable(
                name: "ServiceHealthInsights");
        }
    }
}
