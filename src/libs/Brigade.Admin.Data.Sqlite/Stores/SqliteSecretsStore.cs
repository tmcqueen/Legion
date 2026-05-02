using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data.Sqlite.Stores;

public class SqliteSecretsStore(AppDbContext db) : ISecretsStore
{
    public async Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct);

    public async Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path, ct);

    public async Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default)
    {
        var prefix = parentPath.TrimEnd('/') + "/";
        return await db.Secrets.AsNoTracking()
            .Where(s => s.Path.StartsWith(prefix)
                     && !s.Path.Substring(prefix.Length).Contains('/'))
            .ToListAsync(ct);
    }

    public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
    {
        var secret = new SecretOptions
        {
            Id = SecretOptionsId.New(),
            Path = path,
            Description = description,
            EncryptedValue = plaintext,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Secrets.Add(secret);
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task UpdateValueAsync(Guid id, string plaintext, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is null) return;
        secret.EncryptedValue = plaintext;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateDescriptionAsync(Guid id, string? description, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<string?> DecryptAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        return db.Secrets.AsNoTracking()
            .Where(s => s.Id == typedId)
            .Select(s => (string?)s.EncryptedValue)
            .FirstOrDefaultAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
