using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdToSpoolLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_spool_leases",
                schema: "replicaguard",
                table: "spool_leases");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                schema: "replicaguard",
                table: "spool_leases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "pk_spool_leases",
                schema: "replicaguard",
                table: "spool_leases",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_spool_leases_asset_id",
                schema: "replicaguard",
                table: "spool_leases",
                column: "asset_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_spool_leases",
                schema: "replicaguard",
                table: "spool_leases");

            migrationBuilder.DropIndex(
                name: "ix_spool_leases_asset_id",
                schema: "replicaguard",
                table: "spool_leases");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "replicaguard",
                table: "spool_leases");

            migrationBuilder.AddPrimaryKey(
                name: "pk_spool_leases",
                schema: "replicaguard",
                table: "spool_leases",
                column: "asset_id");
        }
    }
}
