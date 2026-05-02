namespace Brigade.Admin.Data.Models.Agents;

public record WorkflowOptions
{
    public WorkflowOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Content { get; init; }
}
