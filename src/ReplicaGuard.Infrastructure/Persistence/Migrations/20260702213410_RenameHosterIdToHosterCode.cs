using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameHosterIdToHosterCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "hoster_id",
                schema: "replicaguard",
                table: "hoster_accounts",
                newName: "hoster_code");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_hoster_code",
                schema: "replicaguard",
                table: "hosters",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "ix_hoster_accounts_hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts",
                column: "hoster_code");

            migrationBuilder.AddForeignKey(
                name: "fk_hoster_accounts_hoster_hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts",
                column: "hoster_code",
                principalSchema: "replicaguard",
                principalTable: "hosters",
                principalColumn: "code",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hoster_accounts_hoster_hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_hoster_code",
                schema: "replicaguard",
                table: "hosters");

            migrationBuilder.DropIndex(
                name: "ix_hoster_accounts_hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts");

            migrationBuilder.RenameColumn(
                name: "hoster_code",
                schema: "replicaguard",
                table: "hoster_accounts",
                newName: "hoster_id");
        }
    }
}
