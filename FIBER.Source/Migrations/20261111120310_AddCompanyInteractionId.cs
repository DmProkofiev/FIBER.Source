using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIBER.Source.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInteractionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInteractions",
                table: "CompanyInteractions");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CompanyInteractions",
                type: "INTEGER",
                nullable: true);

            // Назначаем уникальные Id существующим записям (используем ROWID как источник уникальности).
            migrationBuilder.Sql(@"
                UPDATE CompanyInteractions SET Id = ROWID WHERE Id IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CompanyInteractions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInteractions",
                table: "CompanyInteractions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UQ_CompanyInteraction_Natural",
                table: "CompanyInteractions",
                columns: new[] { "SourceCompanyId", "TargetCompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInteractions",
                table: "CompanyInteractions");

            migrationBuilder.DropIndex(
                name: "UQ_CompanyInteraction_Natural",
                table: "CompanyInteractions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CompanyInteractions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInteractions",
                table: "CompanyInteractions",
                columns: new[] { "SourceCompanyId", "TargetCompanyId" });
        }
    }
}
