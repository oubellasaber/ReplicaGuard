using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplicaGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeFileNameAsOwnedProp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "file_name",
                schema: "replicaguard",
                table: "assets",
                newName: "FileName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileName",
                schema: "replicaguard",
                table: "assets",
                newName: "file_name");
        }
    }
}
