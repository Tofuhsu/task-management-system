using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class CoverUserSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems",
                columns: new[] { "UserId", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "Title", "Description" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems",
                columns: new[] { "UserId", "CreatedAt" });
        }
    }
}
