using Brigade.Admin.Data.Models;

namespace Brigade.Admin.Data.Stores;

public interface ISecretsStore
{
    Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default);
    Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default);
    Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default);
    Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default);
    Task UpdateValueAsync(int id, string plaintext, CancellationToken ct = default);
    Task UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default);
    Task<string?> DecryptAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
