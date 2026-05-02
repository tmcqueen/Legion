using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legion.Admin.Data.PostgreSQL.Migrations.App
{
    /// <inheritdoc />
    public partial class AddPromptLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsDefaultIncluded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentPromptAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPromptAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentPromptAssignments_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentPromptAssignments_PromptDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "PromptDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Frontmatter = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptVersions_PromptDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "PromptDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_prompt_assignments_agent_definition",
                table: "AgentPromptAssignments",
                columns: new[] { "AgentId", "DefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPromptAssignments_DefinitionId",
                table: "AgentPromptAssignments",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "ix_prompt_definitions_path",
                table: "PromptDefinitions",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prompt_versions_definition_published",
                table: "PromptVersions",
                column: "DefinitionId",
                unique: true,
                filter: "\"Status\" = 'Published'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPromptAssignments");

            migrationBuilder.DropTable(
                name: "PromptVersions");

            migrationBuilder.DropTable(
                name: "PromptDefinitions");
        }
    }
}
