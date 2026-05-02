namespace Brigade.Admin.Data.Models;

public record SecretOptions
{
    public SecretOptionsId Id { get; init; }
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
