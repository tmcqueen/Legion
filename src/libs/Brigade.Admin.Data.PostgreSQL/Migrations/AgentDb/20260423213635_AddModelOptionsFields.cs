using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brigade.Admin.Data.PostgreSQL.Migrations.AgentDb
{
    /// <inheritdoc />
    public partial class AddModelOptionsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CachedTokenCost",
                table: "Models",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ContextWindowSize",
                table: "Models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "InputTokenCost",
                table: "Models",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "KnowledgeCutoff",
                table: "Models",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "Models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "OutputTokenCost",
                table: "Models",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsAudio",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsCodeExecution",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsDistillation",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFileSearch",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFineTuning",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFunctionCalling",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsImage",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsMCP",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStreaming",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStructuredOutput",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsText",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsToolUse",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVideo",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsWebSearch",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedTokenCost",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "ContextWindowSize",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "InputTokenCost",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "KnowledgeCutoff",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "OutputTokenCost",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsAudio",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsCodeExecution",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsDistillation",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsFileSearch",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsFineTuning",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsFunctionCalling",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsImage",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsMCP",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsStreaming",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsStructuredOutput",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsText",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsToolUse",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsVideo",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsWebSearch",
                table: "Models");
        }
    }
}
