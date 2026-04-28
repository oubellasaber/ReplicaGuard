using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlBackedLease_AddRowVersionForAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "replicaguard",
                table: "assets",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "spool_leases",
                schema: "replicaguard",
                columns: table => new
                {
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_replica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spool_leases", x => x.asset_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spool_leases",
                schema: "replicaguard");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "replicaguard",
                table: "assets");
        }
    }
}
