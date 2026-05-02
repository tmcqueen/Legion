namespace Legion.Admin.Data.Seeds.Dtos;

public record SeedAgentDto
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
}
