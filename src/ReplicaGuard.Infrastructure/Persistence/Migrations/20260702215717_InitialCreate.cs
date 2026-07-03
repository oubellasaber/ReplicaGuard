using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "replicaguard");

            migrationBuilder.EnsureSchema(
                name: "transport");

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "jsonb", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hosters",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hosters", x => x.id);
                    table.UniqueConstraint("ak_hoster_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "inbox_state",
                schema: "transport",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    receive_count = table.Column<int>(type: "integer", nullable: false),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_state", x => x.id);
                    table.UniqueConstraint("ak_inbox_state_message_id_consumer_id", x => new { x.message_id, x.consumer_id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_state",
                schema: "transport",
                columns: table => new
                {
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_state", x => x.outbox_id);
                });

            migrationBuilder.CreateTable(
                name: "secret_sets",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_secret_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "spool_leases",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_replica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spool_leases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hoster_accounts",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoster_code = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hoster_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_hoster_accounts_hoster_hoster_code",
                        column: x => x.hoster_code,
                        principalSchema: "replicaguard",
                        principalTable: "hosters",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "transport",
                columns: table => new
                {
                    sequence_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enqueue_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    headers = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    inbox_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inbox_consumer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    initiator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    destination_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    response_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fault_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.sequence_number);
                    table.ForeignKey(
                        name: "fk_outbox_message_inbox_state_inbox_message_id_inbox_consumer_",
                        columns: x => new { x.inbox_message_id, x.inbox_consumer_id },
                        principalSchema: "transport",
                        principalTable: "inbox_state",
                        principalColumns: new[] { "message_id", "consumer_id" });
                    table.ForeignKey(
                        name: "fk_outbox_message_outbox_state_outbox_id",
                        column: x => x.outbox_id,
                        principalSchema: "transport",
                        principalTable: "outbox_state",
                        principalColumn: "outbox_id");
                });

            migrationBuilder.CreateTable(
                name: "secrets",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    encrypted_secret = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    secret_set_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_secrets", x => x.id);
                    table.ForeignKey(
                        name: "fk_secrets_secret_set_secret_set_id",
                        column: x => x.secret_set_id,
                        principalSchema: "replicaguard",
                        principalTable: "secret_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_identities",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    secret_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    hoster_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_identities_hoster_account_hoster_account_id",
                        column: x => x.hoster_account_id,
                        principalSchema: "replicaguard",
                        principalTable: "hoster_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_auth_identities_secret_set_secret_set_id",
                        column: x => x.secret_set_id,
                        principalSchema: "replicaguard",
                        principalTable: "secret_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "replicas",
                schema: "replicaguard",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoster_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    link = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    waiting_for_replica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_replicas_hoster_accounts_hoster_account_id",
                        column: x => x.hoster_account_id,
                        principalSchema: "replicaguard",
                        principalTable: "hoster_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_replicas_hosters_hoster_id",
                        column: x => x.hoster_id,
                        principalSchema: "replicaguard",
                        principalTable: "hosters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_replicas_replicas_waiting_for_replica_id",
                        column: x => x.waiting_for_replica_id,
                        principalSchema: "replicaguard",
                        principalTable: "replicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_user_id",
                schema: "replicaguard",
                table: "assets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_identities_hoster_account_id",
                schema: "replicaguard",
                table: "auth_identities",
                column: "hoster_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_identities_secret_set_id",
                schema: "replicaguard",
                table: "auth_identities",
                column: "secret_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_hoster_accounts_hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts",
                column: "hoster_code");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_state_delivered",
                schema: "transport",
                table: "inbox_state",
                column: "delivered");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_enqueue_time",
                schema: "transport",
                table: "outbox_message",
                column: "enqueue_time");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_expiration_time",
                schema: "transport",
                table: "outbox_message",
                column: "expiration_time");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_inbox_message_id_inbox_consumer_id_sequence_",
                schema: "transport",
                table: "outbox_message",
                columns: new[] { "inbox_message_id", "inbox_consumer_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_outbox_id_sequence_number",
                schema: "transport",
                table: "outbox_message",
                columns: new[] { "outbox_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_state_created",
                schema: "transport",
                table: "outbox_state",
                column: "created");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_asset_id",
                schema: "replicaguard",
                table: "replicas",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_hoster_account_id",
                schema: "replicaguard",
                table: "replicas",
                column: "hoster_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_hoster_id",
                schema: "replicaguard",
                table: "replicas",
                column: "hoster_id");

            migrationBuilder.CreateIndex(
                name: "ix_replicas_waiting_for_replica_id",
                schema: "replicaguard",
                table: "replicas",
                column: "waiting_for_replica_id");

            migrationBuilder.CreateIndex(
                name: "ix_secrets_secret_set_id",
                schema: "replicaguard",
                table: "secrets",
                column: "secret_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_spool_leases_asset_id",
                schema: "replicaguard",
                table: "spool_leases",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at_utc",
                schema: "replicaguard",
                table: "users",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "replicaguard",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_identity_id",
                schema: "replicaguard",
                table: "users",
                column: "identity_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_identities",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "replicas",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "secrets",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "spool_leases",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "users",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "inbox_state",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "outbox_state",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "hoster_accounts",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "secret_sets",
                schema: "replicaguard");

            migrationBuilder.DropTable(
                name: "hosters",
                schema: "replicaguard");
        }
    }
}
