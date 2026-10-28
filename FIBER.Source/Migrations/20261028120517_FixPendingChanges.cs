using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIBER.Source.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonRelationship_Person",
                table: "PersonRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonRelationship_RelatedPerson",
                table: "PersonRelationships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PersonRelationships",
                table: "PersonRelationships");

            migrationBuilder.DropIndex(
                name: "IX_Person_FullName_Identified",
                table: "People");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_Status_FullName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "RelationType",
                table: "PersonRelationships");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "People");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "PersonRelationships",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "PersonRelationships",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "People",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "People",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "People",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "SecondName",
                table: "People",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EventParticipants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PersonRelationships",
                table: "PersonRelationships",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships",
                columns: new[] { "PersonId", "RelatedPersonId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_Name_Identified",
                table: "People",
                columns: new[] { "LastName", "FirstName" },
                filter: "\"Status\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_Status_FullName",
                table: "People",
                sql: "(Status = 0 AND LastName IS NULL AND FirstName IS NULL AND SecondName IS NULL) OR (Status = 1 AND LastName IS NOT NULL AND FirstName IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRelationship_Person",
                table: "PersonRelationships",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRelationship_RelatedPerson",
                table: "PersonRelationships",
                column: "RelatedPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonRelationship_Person",
                table: "PersonRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonRelationship_RelatedPerson",
                table: "PersonRelationships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PersonRelationships",
                table: "PersonRelationships");

            migrationBuilder.DropIndex(
                name: "UQ_PersonRelationship_Natural",
                table: "PersonRelationships");

            migrationBuilder.DropIndex(
                name: "IX_Person_Name_Identified",
                table: "People");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_Status_FullName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PersonRelationships");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PersonRelationships");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "SecondName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "EventParticipants");

            migrationBuilder.AddColumn<string>(
                name: "RelationType",
                table: "PersonRelationships",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "People",
                type: "TEXT",
                maxLength: 200,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PersonRelationships",
                table: "PersonRelationships",
                columns: new[] { "PersonId", "RelatedPersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_Person_FullName_Identified",
                table: "People",
                column: "FullName",
                filter: "\"Status\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_Status_FullName",
                table: "People",
                sql: "(Status = 0 AND FullName IS NULL) OR (Status = 1 AND FullName IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRelationship_Person",
                table: "PersonRelationships",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRelationship_RelatedPerson",
                table: "PersonRelationships",
                column: "RelatedPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
