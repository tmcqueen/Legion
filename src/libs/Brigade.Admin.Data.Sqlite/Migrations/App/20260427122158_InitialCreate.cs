using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brigade.Admin.Data.Sqlite.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agents");

            migrationBuilder.CreateTable(
                name: "McpServers",
                schema: "agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ServerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ServerLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Transport = table.Column<int>(type: "INTEGER", nullable: false),
                    RequireApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommandLine = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Middlewares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Middlewares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    ContextWindowSize = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    KnowledgeCutoff = table.Column<string>(type: "TEXT", nullable: true),
                    SupportsText = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsImage = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsAudio = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsStructuredOutput = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsFineTuning = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsDistillation = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsToolUse = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsWebSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsFileSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsCodeExecution = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsMCP = table.Column<bool>(type: "INTEGER", nullable: false),
                    InputTokenCost = table.Column<double>(type: "REAL", nullable: false),
                    CachedTokenCost = table.Column<double>(type: "REAL", nullable: false),
                    OutputTokenCost = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                schema: "agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ApiToken = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    License = table.Column<string>(type: "TEXT", nullable: true),
                    Compatibility = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedTools = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Resources = table.Column<string>(type: "TEXT", nullable: true),
                    Scripts = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ParametersSchema = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpServerHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    McpServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServerHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpServerHeaders_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalSchema: "agents",
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Instructions = table.Column<string>(type: "TEXT", nullable: true),
                    MaxTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agents_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "agents",
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderModels",
                schema: "agents",
                columns: table => new
                {
                    ModelsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProvidersId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderModels", x => new { x.ModelsId, x.ProvidersId });
                    table.ForeignKey(
                        name: "FK_ProviderModels_Models_ModelsId",
                        column: x => x.ModelsId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderModels_Providers_ProvidersId",
                        column: x => x.ProvidersId,
                        principalSchema: "agents",
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentMcpServers",
                schema: "agents",
                columns: table => new
                {
                    AgentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    McpServersId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMcpServers", x => new { x.AgentsId, x.McpServersId });
                    table.ForeignKey(
                        name: "FK_AgentMcpServers_Agents_AgentsId",
                        column: x => x.AgentsId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentMcpServers_McpServers_McpServersId",
                        column: x => x.McpServersId,
                        principalSchema: "agents",
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentMiddleware",
                schema: "agents",
                columns: table => new
                {
                    AgentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    MiddlewareId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMiddleware", x => new { x.AgentsId, x.MiddlewareId });
                    table.ForeignKey(
                        name: "FK_AgentMiddleware_Agents_AgentsId",
                        column: x => x.AgentsId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentMiddleware_Middlewares_MiddlewareId",
                        column: x => x.MiddlewareId,
                        principalTable: "Middlewares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentModels",
                schema: "agents",
                columns: table => new
                {
                    AgentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentModels", x => new { x.AgentsId, x.ModelsId });
                    table.ForeignKey(
                        name: "FK_AgentModels_Agents_AgentsId",
                        column: x => x.AgentsId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentModels_Models_ModelsId",
                        column: x => x.ModelsId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSkills",
                schema: "agents",
                columns: table => new
                {
                    AgentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSkills", x => new { x.AgentsId, x.SkillsId });
                    table.ForeignKey(
                        name: "FK_AgentSkills_Agents_AgentsId",
                        column: x => x.AgentsId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentSkills_Skills_SkillsId",
                        column: x => x.SkillsId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentTools",
                schema: "agents",
                columns: table => new
                {
                    AgentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToolsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTools", x => new { x.AgentsId, x.ToolsId });
                    table.ForeignKey(
                        name: "FK_AgentTools_Agents_AgentsId",
                        column: x => x.AgentsId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentTools_Tools_ToolsId",
                        column: x => x.ToolsId,
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTime = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxResults = table.Column<int>(type: "INTEGER", nullable: true),
                    FunctionToolName = table.Column<string>(type: "TEXT", nullable: true),
                    FunctionToolDescription = table.Column<string>(type: "TEXT", nullable: true),
                    ContextPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    StateKey = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memories_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMcpServers_McpServersId",
                schema: "agents",
                table: "AgentMcpServers",
                column: "McpServersId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMiddleware_MiddlewareId",
                schema: "agents",
                table: "AgentMiddleware",
                column: "MiddlewareId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentModels_ModelsId",
                schema: "agents",
                table: "AgentModels",
                column: "ModelsId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ProviderId",
                table: "Agents",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_SkillsId",
                schema: "agents",
                table: "AgentSkills",
                column: "SkillsId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_ToolsId",
                schema: "agents",
                table: "AgentTools",
                column: "ToolsId");

            migrationBuilder.CreateIndex(
                name: "IX_McpServerHeaders_McpServerId",
                table: "McpServerHeaders",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_AgentId",
                table: "Memories",
                column: "AgentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderModels_ProvidersId",
                schema: "agents",
                table: "ProviderModels",
                column: "ProvidersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMcpServers",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "AgentMiddleware",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "AgentModels",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "AgentSkills",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "AgentTools",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "McpServerHeaders");

            migrationBuilder.DropTable(
                name: "Memories");

            migrationBuilder.DropTable(
                name: "ProviderModels",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropTable(
                name: "Middlewares");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Tools");

            migrationBuilder.DropTable(
                name: "McpServers",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "Providers",
                schema: "agents");
        }
    }
}
