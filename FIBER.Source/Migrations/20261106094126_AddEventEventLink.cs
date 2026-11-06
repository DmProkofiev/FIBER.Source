using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIBER.Source.Migrations
{
    /// <inheritdoc />
    public partial class AddEventEventLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationshipType = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventEvent_SourceEvent",
                        column: x => x.SourceEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventEvent_TargetEvent",
                        column: x => x.TargetEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventEvent_TargetEventId",
                table: "EventEvents",
                column: "TargetEventId");

            migrationBuilder.CreateIndex(
                name: "UQ_EventEvent_Natural",
                table: "EventEvents",
                columns: new[] { "SourceEventId", "TargetEventId", "RelationshipType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventEvents");
        }
    }
}
