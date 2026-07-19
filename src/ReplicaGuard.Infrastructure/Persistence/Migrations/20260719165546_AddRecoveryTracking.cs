using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_recovery_attempt_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recovery_attempt_count",
                schema: "replicaguard",
                table: "replicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_recovery_attempt_at_utc",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "recovery_attempt_count",
                schema: "replicaguard",
                table: "replicas");
        }
    }
}
