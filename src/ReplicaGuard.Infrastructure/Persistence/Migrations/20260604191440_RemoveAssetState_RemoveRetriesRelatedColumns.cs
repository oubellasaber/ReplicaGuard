using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    public partial class RemoveAssetState_RemoveRetriesRelatedColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop index on assets.state
            migrationBuilder.DropIndex(
                name: "ix_assets_state",
                schema: "replicaguard",
                table: "assets");

            // Drop the state column from assets entirely
            migrationBuilder.DropColumn(
                name: "state",
                schema: "replicaguard",
                table: "assets");

            // Remove retry-related columns
            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "retry_count",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "replicaguard",
                table: "assets");

            // Rename replicas.state → replicas.status
            migrationBuilder.RenameColumn(
                name: "state",
                schema: "replicaguard",
                table: "replicas",
                newName: "status");

            migrationBuilder.RenameIndex(
                name: "ix_replicas_state",
                schema: "replicaguard",
                table: "replicas",
                newName: "ix_replicas_status");

            // Fix replica timestamps
            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP AT TIME ZONE 'UTC'",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore replicas.status → replicas.state
            migrationBuilder.RenameColumn(
                name: "status",
                schema: "replicaguard",
                table: "replicas",
                newName: "state");

            migrationBuilder.RenameIndex(
                name: "ix_replicas_status",
                schema: "replicaguard",
                table: "replicas",
                newName: "ix_replicas_state");

            // Restore assets.state column
            migrationBuilder.AddColumn<int>(
                name: "state",
                schema: "replicaguard",
                table: "assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Restore timestamps
            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

            // Restore retry-related columns
            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "replicaguard",
                table: "replicas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                schema: "replicaguard",
                table: "replicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "replicaguard",
                table: "assets",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            // Restore index on assets.state
            migrationBuilder.CreateIndex(
                name: "ix_assets_state",
                schema: "replicaguard",
                table: "assets", 
                column: "state");
        }
    }
}
