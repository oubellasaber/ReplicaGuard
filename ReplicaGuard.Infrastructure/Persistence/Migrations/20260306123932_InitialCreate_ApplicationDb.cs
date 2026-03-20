using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ApplicationDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "hosters",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<long>(
                name: "version",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "sync_status",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "jsonb", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "replicas",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    link = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    waiting_for_replica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_replicas", x => x.id);
                    table.ForeignKey(
                        name: "fk_replicas_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "replicaguard",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at_utc",
                schema: "replicaguard",
                table: "users",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_hosters_created_at_utc",
                schema: "replicaguard",
                table: "hosters",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_hoster_credentials_created_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_hoster_credentials_sync_status",
                schema: "replicaguard",
                table: "hoster_credentials",
                column: "sync_status");

            migrationBuilder.CreateIndex(
                name: "ix_hoster_credentials_updated_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials",
                column: "updated_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_assets_created_at_utc",
                schema: "replicaguard",
                table: "assets",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_assets_state",
                schema: "replicaguard",
                table: "assets",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_assets_user_id",
                schema: "replicaguard",
                table: "assets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_asset_id",
                schema: "replicaguard",
                table: "replicas",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_asset_id_hoster_id",
                schema: "replicaguard",
                table: "replicas",
                columns: new[] { "asset_id", "hoster_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_replicas_hoster_id",
                schema: "replicaguard",
                table: "replicas",
                column: "hoster_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_state",
                schema: "replicaguard",
                table: "replicas",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "replicas",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "replicaguard");

            migrationBuilder.DropIndex(
                name: "ix_users_created_at_utc",
                schema: "replicaguard",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_hosters_created_at_utc",
                schema: "replicaguard",
                table: "hosters");

            migrationBuilder.DropIndex(
                name: "ix_hoster_credentials_created_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials");

            migrationBuilder.DropIndex(
                name: "ix_hoster_credentials_sync_status",
                schema: "replicaguard",
                table: "hoster_credentials");

            migrationBuilder.DropIndex(
                name: "ix_hoster_credentials_updated_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "hosters",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<long>(
                name: "version",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 1L);

            migrationBuilder.AlterColumn<int>(
                name: "sync_status",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                schema: "replicaguard",
                table: "hoster_credentials",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
