using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskManager.Api.Data;

#nullable disable

namespace TaskManager.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917000000_AddAuthenticationAndOwnership")]
public partial class AddAuthenticationAndOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "TaskItems",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "TaskItems",
            type: "int",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Title",
            table: "TaskItems",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "TaskItems",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "Priority",
            table: "TaskItems",
            type: "int",
            nullable: false,
            defaultValue: 2,
            oldClrType: typeof(int),
            oldType: "int",
            oldDefaultValue: 0);

        migrationBuilder.AlterColumn<bool>(
            name: "IsCompleted",
            table: "TaskItems",
            type: "bit",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "bit");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "TaskItems",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "SYSUTCDATETIME()",
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.Sql(
            "IF EXISTS (SELECT 1 FROM [TaskItems]) " +
            "THROW 50001, 'Existing tasks must be assigned to a user before applying the authentication migration.', 1;");

        migrationBuilder.AlterColumn<int>(
            name: "UserId",
            table: "TaskItems",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_CreatedAt",
            table: "TaskItems",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_DueDate",
            table: "TaskItems",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_Priority",
            table: "TaskItems",
            column: "Priority");

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_Status",
            table: "TaskItems",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_Status_Priority_DueDate",
            table: "TaskItems",
            columns: new[] { "Status", "Priority", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_UserId_CreatedAt",
            table: "TaskItems",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_TaskItems_UserId_Status_Priority_DueDate",
            table: "TaskItems",
            columns: new[] { "UserId", "Status", "Priority", "DueDate" });

        migrationBuilder.AddForeignKey(
            name: "FK_TaskItems_Users_UserId",
            table: "TaskItems",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TaskItems_Users_UserId",
            table: "TaskItems");

        migrationBuilder.DropTable(name: "Users");

        migrationBuilder.DropIndex(name: "IX_TaskItems_CreatedAt", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_DueDate", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_Priority", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_Status", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_Status_Priority_DueDate", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_UserId_CreatedAt", table: "TaskItems");
        migrationBuilder.DropIndex(name: "IX_TaskItems_UserId_Status_Priority_DueDate", table: "TaskItems");

        migrationBuilder.DropColumn(name: "Status", table: "TaskItems");
        migrationBuilder.DropColumn(name: "UserId", table: "TaskItems");

        migrationBuilder.AlterColumn<string>(
            name: "Title",
            table: "TaskItems",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "TaskItems",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "Priority",
            table: "TaskItems",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldDefaultValue: 2);

        migrationBuilder.AlterColumn<bool>(
            name: "IsCompleted",
            table: "TaskItems",
            type: "bit",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "bit",
            oldDefaultValue: false);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "TaskItems",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "SYSUTCDATETIME()");
    }
}
