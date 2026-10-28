using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FIBER.Source.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActionObject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, collation: "NOCASE"),
                    RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Mail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    NameRu = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "MediaAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyInteractions",
                columns: table => new
                {
                    SourceCompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetCompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    InteractionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInteractions", x => new { x.SourceCompanyId, x.TargetCompanyId });
                    table.ForeignKey(
                        name: "FK_CompanyInteraction_Source",
                        column: x => x.SourceCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyInteraction_Target",
                        column: x => x.TargetCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Passport = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Job = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    Car = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Mail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SocialMedia = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MergedIntoPersonId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                    table.CheckConstraint("CK_Person_Status_FullName", "(Status = 0 AND FullName IS NULL) OR (Status = 1 AND FullName IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Person_Country",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Person_MergedInto",
                        column: x => x.MergedIntoPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaAttachmentLinks",
                columns: table => new
                {
                    MediaAttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerType = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAttachmentLinks", x => new { x.MediaAttachmentId, x.OwnerType, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_MediaAttachmentLink_Media",
                        column: x => x.MediaAttachmentId,
                        principalTable: "MediaAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, collation: "NOCASE"),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizerCompanyId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizerPersonId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Event_OrganizerCompany",
                        column: x => x.OrganizerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Event_OrganizerPerson",
                        column: x => x.OrganizerPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PersonAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonAlias_Person",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonRelationships",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedPersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRelationships", x => new { x.PersonId, x.RelatedPersonId });
                    table.ForeignKey(
                        name: "FK_PersonRelationship_Person",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonRelationship_RelatedPerson",
                        column: x => x.RelatedPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipants", x => x.Id);
                    table.CheckConstraint("CK_EventParticipant_OneOwner", "(PersonId IS NOT NULL AND CompanyId IS NULL) OR (PersonId IS NULL AND CompanyId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_EventParticipant_Company",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventParticipant_Event",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventParticipant_Person",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Code", "IsActive", "NameEn", "NameRu" },
                values: new object[,]
                {
                    { "AE", true, "United Arab Emirates", "ОАЭ" },
                    { "AM", true, "Armenia", "Армения" },
                    { "AR", true, "Argentina", "Аргентина" },
                    { "AU", true, "Australia", "Австралия" },
                    { "AZ", true, "Azerbaijan", "Азербайджан" },
                    { "BR", true, "Brazil", "Бразилия" },
                    { "BY", true, "Belarus", "Беларусь" },
                    { "CA", true, "Canada", "Канада" },
                    { "CN", true, "China", "Китай" },
                    { "DE", true, "Germany", "Германия" },
                    { "EE", true, "Estonia", "Эстония" },
                    { "ES", true, "Spain", "Испания" },
                    { "FR", true, "France", "Франция" },
                    { "GB", true, "United Kingdom", "Великобритания" },
                    { "GE", true, "Georgia", "Грузия" },
                    { "IL", true, "Israel", "Израиль" },
                    { "IN", true, "India", "Индия" },
                    { "IT", true, "Italy", "Италия" },
                    { "JP", true, "Japan", "Япония" },
                    { "KG", true, "Kyrgyzstan", "Кыргызстан" },
                    { "KR", true, "South Korea", "Южная Корея" },
                    { "KZ", true, "Kazakhstan", "Казахстан" },
                    { "LT", true, "Lithuania", "Литва" },
                    { "LV", true, "Latvia", "Латвия" },
                    { "MD", true, "Moldova", "Молдова" },
                    { "RU", true, "Russia", "Россия" },
                    { "TJ", true, "Tajikistan", "Таджикистан" },
                    { "TR", true, "Turkey", "Турция" },
                    { "UA", true, "Ukraine", "Украина" },
                    { "US", true, "United States", "США" },
                    { "UZ", true, "Uzbekistan", "Узбекистан" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "AuditLogEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Company_Name",
                table: "Companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInteraction_TargetCompanyId",
                table: "CompanyInteractions",
                column: "TargetCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_CompanyId",
                table: "EventParticipants",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_PersonId",
                table: "EventParticipants",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_EventParticipant_EventCompany",
                table: "EventParticipants",
                columns: new[] { "EventId", "CompanyId" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_EventParticipant_EventPerson",
                table: "EventParticipants",
                columns: new[] { "EventId", "PersonId" },
                unique: true,
                filter: "\"PersonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Event_Date",
                table: "Events",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Event_OrganizerCompanyId",
                table: "Events",
                column: "OrganizerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Event_OrganizerPersonId",
                table: "Events",
                column: "OrganizerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Event_Title",
                table: "Events",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAttachmentLink_Owner",
                table: "MediaAttachmentLinks",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAttachment_CreatedAt",
                table: "MediaAttachments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "UQ_MediaAttachment_ContentHash",
                table: "MediaAttachments",
                column: "ContentHash",
                unique: true,
                filter: "\"ContentHash\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_People_CountryCode",
                table: "People",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_People_MergedIntoPersonId",
                table: "People",
                column: "MergedIntoPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_BirthDate",
                table: "People",
                column: "BirthDate",
                filter: "\"BirthDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Person_FullName_Identified",
                table: "People",
                column: "FullName",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Person_Status_CreatedAt",
                table: "People",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAlias_PersonId_IsPrimary",
                table: "PersonAliases",
                columns: new[] { "PersonId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAlias_Value",
                table: "PersonAliases",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "UQ_PersonAlias_Primary",
                table: "PersonAliases",
                column: "PersonId",
                unique: true,
                filter: "\"IsPrimary\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRelationship_RelatedPersonId",
                table: "PersonRelationships",
                column: "RelatedPersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "CompanyInteractions");

            migrationBuilder.DropTable(
                name: "EventParticipants");

            migrationBuilder.DropTable(
                name: "MediaAttachmentLinks");

            migrationBuilder.DropTable(
                name: "PersonAliases");

            migrationBuilder.DropTable(
                name: "PersonRelationships");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "MediaAttachments");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
