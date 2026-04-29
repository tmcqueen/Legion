using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Brigade.Admin.Data.PostgreSQL.Stores;

public class PostgreSqlSecretsStore(AppDbContext db, IConfiguration config) : ISecretsStore
{
    private string EncryptionKey => config["Secrets:EncryptionKey"]
        ?? throw new InvalidOperationException("Secrets:EncryptionKey is not configured.");

    public async Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct);

    public async Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path, ct);

    public async Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default)
    {
        var prefix = parentPath.TrimEnd('/') + "/";
        var prefixPattern = prefix + "%";
        var nestedPattern = prefix + "%/%";
        return await db.Secrets.AsNoTracking()
            .Where(s => EF.Functions.Like(s.Path, prefixPattern)
                     && !EF.Functions.Like(s.Path, nestedPattern))
            .ToListAsync(ct);
    }

    public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Secrets" ("Path", "Description", "EncryptedValue", "CreatedAt", "UpdatedAt")
            VALUES ({0}, {1}, pgp_sym_encrypt({2}, {3})::text, NOW(), NOW())
            """,
            [path, description as object ?? DBNull.Value, plaintext, key], ct);

        return await db.Secrets.AsNoTracking().FirstAsync(s => s.Path == path, ct);
    }

    public async Task UpdateValueAsync(int id, string plaintext, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Secrets" SET "EncryptedValue" = pgp_sym_encrypt({0}, {1})::text, "UpdatedAt" = NOW()
            WHERE "Id" = {2}
            """,
            [plaintext, key, id], ct);
    }

    public async Task UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> DecryptAsync(int id, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        var results = await db.Database
            .SqlQueryRaw<string>(
                """SELECT pgp_sym_decrypt("EncryptedValue"::bytea, {0}) AS "Value" FROM "Secrets" WHERE "Id" = {1}""",
                key, id)
            .ToListAsync(ct);
        return results.FirstOrDefault();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
