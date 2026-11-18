using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIBER.Source.Migrations
{
    /// <inheritdoc />
    public partial class BidirectionalRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyPeople_Companies_CompanyId",
                table: "CompanyPeople");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyPeople_People_PersonId",
                table: "CompanyPeople");

            migrationBuilder.DropIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships");

            migrationBuilder.DropIndex(
                name: "IX_CompanyPeople_CompanyId",
                table: "CompanyPeople");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "CompanyPeople");

            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "PersonRelationships",
                newName: "KindTo");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyPeople_PersonId",
                table: "CompanyPeople",
                newName: "IX_CompanyPerson_PersonId");

            migrationBuilder.AddColumn<int>(
                name: "KindFrom",
                table: "PersonRelationships",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRole",
                table: "CompanyPeople",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonRole",
                table: "CompanyPeople",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InteractionTypeTo",
                table: "CompanyInteractions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships",
                columns: new[] { "PersonId", "RelatedPersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_CompanyPerson_Natural",
                table: "CompanyPeople",
                columns: new[] { "CompanyId", "PersonId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPerson_Company",
                table: "CompanyPeople",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPerson_Person",
                table: "CompanyPeople",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyPerson_Company",
                table: "CompanyPeople");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyPerson_Person",
                table: "CompanyPeople");

            migrationBuilder.DropIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships");

            migrationBuilder.DropIndex(
                name: "UQ_CompanyPerson_Natural",
                table: "CompanyPeople");

            migrationBuilder.DropColumn(
                name: "KindFrom",
                table: "PersonRelationships");

            migrationBuilder.DropColumn(
                name: "CompanyRole",
                table: "CompanyPeople");

            migrationBuilder.DropColumn(
                name: "PersonRole",
                table: "CompanyPeople");

            migrationBuilder.DropColumn(
                name: "InteractionTypeTo",
                table: "CompanyInteractions");

            migrationBuilder.RenameColumn(
                name: "KindTo",
                table: "PersonRelationships",
                newName: "Kind");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyPerson_PersonId",
                table: "CompanyPeople",
                newName: "IX_CompanyPeople_PersonId");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "CompanyPeople",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships",
                columns: new[] { "PersonId", "RelatedPersonId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPeople_CompanyId",
                table: "CompanyPeople",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPeople_Companies_CompanyId",
                table: "CompanyPeople",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPeople_People_PersonId",
                table: "CompanyPeople",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
