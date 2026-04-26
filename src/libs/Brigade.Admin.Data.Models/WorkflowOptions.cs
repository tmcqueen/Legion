namespace Brigade.Admin.Data.Models.Agents;

public record WorkflowOptions
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Content { get; init; }
}
