
namespace Brigade.Admin.Data.Models.Agents;

public record AgentTemplate : AgentOptions
{
    public string? TemplateName { get; set; }
    public string? TemplateDescription { get; set; }
}