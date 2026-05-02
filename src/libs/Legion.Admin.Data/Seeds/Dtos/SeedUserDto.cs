namespace Legion.Admin.Data.Seeds.Dtos;

public record SeedUserDto
{
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool EmailConfirmed { get; init; }
    public string Password { get; init; } = "";
}
