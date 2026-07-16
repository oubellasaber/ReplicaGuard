using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiryToReplica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "availability_status",
                schema: "replicaguard",
                table: "replicas",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_expiration_check_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "predicted_expiry_at_utc",
                schema: "replicaguard",
                table: "replicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_replica_id",
                schema: "replicaguard",
                table: "replicas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_replicas_availability_status",
                schema: "replicaguard",
                table: "replicas",
                column: "availability_status");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_predicted_expiry_at_utc",
                schema: "replicaguard",
                table: "replicas",
                column: "predicted_expiry_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_source_replica_id",
                schema: "replicaguard",
                table: "replicas",
                column: "source_replica_id");

            migrationBuilder.AddForeignKey(
                name: "fk_replicas_replicas_source_replica_id",
                schema: "replicaguard",
                table: "replicas",
                column: "source_replica_id",
                principalSchema: "replicaguard",
                principalTable: "replicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_replicas_replicas_source_replica_id",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropIndex(
                name: "ix_replicas_availability_status",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropIndex(
                name: "ix_replicas_predicted_expiry_at_utc",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropIndex(
                name: "ix_replicas_source_replica_id",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "availability_status",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "last_expiration_check_at_utc",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "predicted_expiry_at_utc",
                schema: "replicaguard",
                table: "replicas");

            migrationBuilder.DropColumn(
                name: "source_replica_id",
                schema: "replicaguard",
                table: "replicas");
        }
    }
}
