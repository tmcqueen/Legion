# Secrets Management — Design Spec

**Date:** 2026-04-27  
**Branch:** features/secrets-management  
**Status:** Approved

---

## Context

Brigade stores sensitive values — provider API keys (`ProviderOptions.ApiToken`), MCP server header values (`McpServerHeaders.Value`), and MCP server command-line strings (`McpServerOptions.CommandLine`) — as plain strings in the database. There is no mechanism to manage these values centrally, rotate them, or protect them at rest.

This feature introduces a named, path-addressable secrets registry. Resources reference secrets by URI rather than storing raw values. In production (PostgreSQL) secret values are encrypted at rest using the `pgcrypto` extension; in development (SQLite) they are stored as plaintext for simplicity.

---

## Design

### 1. Data Model

A new `SecretOptions` record is added to `Brigade.Admin.Data.Models`. **No changes are made to existing models** (`ProviderOptions`, `McpServerOptions`, `McpServerHeaders`). Those fields remain `string?` and will hold either a raw value or a `secret://` URI.

```csharp
// src/libs/Brigade.Admin.Data.Models/SecretOptions.cs
public record SecretOptions {
    public int Id { get; init; }
    public string Path { get; set; } = string.Empty;   // e.g. "openai/client_ids/test"
    public string? Description { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- The full secret URI is `secret://{Path}`.
- `AppDbContext` gains `DbSet<SecretOptions> Secrets`.
- An EF migration adds the `Secrets` table only; existing tables are untouched.

**Collection queries** retrieve direct children only (one level deep):

```sql
WHERE path LIKE 'openai/client_ids/%'
  AND path NOT LIKE 'openai/client_ids/%/%'
```

If a collection path has mixed children (some leaves, some sub-folders), only leaf entries are returned in resolution results — sub-folder paths have no value to return. The tree UI handles sub-folders as navigation nodes, not resolvable secrets.

---

### 2. Encryption Strategy

#### PostgreSQL (production)
Encryption is performed inside the database using the `pgcrypto` extension:

- **Write:** `pgp_sym_encrypt(plaintext, passphrase)` — returns `bytea`
- **Read:** `pgp_sym_decrypt(ciphertext, passphrase)` — returns `text`
- The passphrase is read from configuration key `Secrets:EncryptionKey` (environment variable or app settings).
- The `EncryptedValue` column is `bytea`.
- Because pgcrypto functions must run inside PostgreSQL, the PostgreSQL secrets store uses raw SQL (`ExecuteSqlRawAsync` / `FromSqlRaw`) rather than standard EF CRUD for all encrypt/decrypt operations.

#### SQLite (development/test)
- Values are stored as plaintext `text`.
- Normal EF Core operations; no encryption applied.

#### Provider-specific stores

Two implementations of `ISecretsStore` are registered by each DB provider's extension:

| Project | Implementation | Encryption |
|---|---|---|
| `Brigade.Admin.Data.PostgreSQL` | `PostgreSqlSecretsStore` | pgcrypto raw SQL |
| `Brigade.Admin.Data.Sqlite` | `SqliteSecretsStore` | EF Core, plaintext |

Both implement a shared `ISecretsStore` interface that follows the existing `IStore<T>` pattern.

---

### 3. Resolution Service (`ISecretsManager`)

A scoped `ISecretsManager` is registered in DI and available on every HTTP request.

```csharp
public record SecretRequest {
    public string Path { get; init; } = string.Empty;   // full secret:// URI
    public string MediaType { get; init; } = "text/plain";
}

public interface ISecretsManager {
    bool IsSecretReference(string? value);
    Task<string?> ResolveAsync(SecretRequest request, CancellationToken ct = default);
}
```

**`IsSecretReference`** returns `true` when the value starts with `secret://`.

**`ResolveAsync` resolution matrix:**

| Path type | MediaType | Returns |
|---|---|---|
| Leaf (`openai/client_ids/test`) | `text/plain` | `"foo"` |
| Leaf | `text/json` | `{"key":"test","value":"foo"}` |
| Collection (`openai/client_ids`) | `text/json` | `[{"key":"test","value":"foo"},…]` |
| Collection | `text/plain` | `test=foo&other=bar` (URL-encoded key=value pairs) |

The implementation parses the `secret://` URI, determines whether the path is a leaf (exact match) or collection (has children via prefix query), decrypts via `ISecretsStore`, and formats the result.

---

### 4. Outbound HTTP Middleware (`SecretResolvingHandler`)

A `DelegatingHandler` is added to every `HttpClient` that Brigade uses to call AI providers. Before each outbound request is sent, the handler scans all request headers for `secret://` references and resolves them via `ISecretsManager`.

```csharp
public class SecretResolvingHandler(ISecretsManager secrets) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var (key, values) in request.Headers.ToList())
        {
            var resolved = new List<string>();
            foreach (var v in values)
            {
                resolved.Add(secrets.IsSecretReference(v)
                    ? await secrets.ResolveAsync(new SecretRequest { Path = v }, ct) ?? v
                    : v);
            }
            request.Headers.Remove(key);
            request.Headers.TryAddWithoutValidation(key, resolved);
        }
        return await base.SendAsync(request, ct);
    }
}
```

- Registered as **transient** to avoid captive-dependency issues with the scoped `ISecretsManager`.
- Added to named `HttpClient` instances used by `AgentFactory` in `Brigade.Agents`.
- `Brigade.Agents.csproj` gains a project reference to `Brigade.Admin.Data` to access `ISecretsManager`.

---

### 5. UI (WebDev / Blazor)

#### Secrets Management page — `/admin/secrets`

The page is split into two panels:
- **Left:** `RadzenTree` showing the secret path hierarchy
- **Right:** Detail/edit panel for the selected node

The flat list of `SecretOptions` is transformed client-side into a `SecretTreeNode` tree by splitting each `Path` on `/`. Folder nodes are synthetic groupings; leaf nodes correspond to actual `SecretOptions` records.

```
secret://
  openai/                ← folder node
    client_ids/          ← folder node
      test               ← leaf node (SecretOptions)
    api_key              ← leaf node
```

The tree uses `HasChildren`, the `Expand` event for lazy child display, and `tree.Reload()` after create/edit/delete. A `RadzenTextBox` with `@oninput` filters the bound data in real-time by matching the search term against full paths.

**Context menu (right-click):**
- Folder node: "Add secret here" (pre-fills path prefix in create form)
- Leaf node: "Edit description", "Replace value", "Reveal", "Delete"

**Detail panel** shows the selected secret's full path, description, and masked value (`•••`).  
**Reveal:** calls `POST /api/secrets/{id}/reveal` (admin-only), displays plaintext inline for 30 seconds, then re-masks.

#### Provider / MCP Server forms

Wherever `ApiToken`, `McpServerHeaders.Value`, or `McpServerOptions.CommandLine` appear in edit forms, a mode toggle is added:
- **Raw mode:** existing plain text input
- **Secret mode:** path input or searchable dropdown of known `secret://` URIs

In Secret mode the stored value is the `secret://` URI string. The form displays the path with a lock icon — the plaintext value is never fetched just to render the form.

---

### 6. Admin API

`POST /api/secrets/{id}/reveal`
- Requires admin role.
- Decrypts and returns the plaintext value for the specified secret.
- Response: `{ "value": "plaintext" }`

---

## File Inventory

### New files
| File | Purpose |
|---|---|
| `src/libs/Brigade.Admin.Data.Models/SecretOptions.cs` | Data model |
| `src/libs/Brigade.Admin.Data/Stores/SecretsStore.cs` | `ISecretsStore` interface + base |
| `src/libs/Brigade.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs` | pgcrypto store |
| `src/libs/Brigade.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs` | Plaintext store |
| `src/libs/Brigade.Admin.Data/Services/SecretsManager.cs` | `ISecretsManager` implementation |
| `src/Brigade.Agents/Http/SecretResolvingHandler.cs` | Outbound HTTP handler |
| `src/WebDev/Components/Pages/Secrets.razor` | Secrets management page |
| `src/WebDev/Controllers/SecretsController.cs` | Reveal API endpoint |
| EF migrations for PostgreSQL and SQLite | `Secrets` table |

### Modified files
| File | Change |
|---|---|
| `src/libs/Brigade.Admin.Data/AppDbContext.cs` | Add `DbSet<SecretOptions>` |
| `src/libs/Brigade.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs` | Register `PostgreSqlSecretsStore` |
| `src/libs/Brigade.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs` | Register `SqliteSecretsStore` |
| `src/libs/Brigade.Admin.Data/Extensions/AgentDbContextExtensions.cs` | Register `ISecretsManager` |
| `src/Brigade.Agents/Brigade.Agents.csproj` | Add reference to `Brigade.Admin.Data` |
| `src/Brigade.Agents/Providers/AgentFactory.cs` | Accept `ISecretsManager`, resolve secrets before building agents |
| Provider / MCP Server edit forms in `Brigade.Admin.UI` | Add secret mode toggle |

---

## Out of Scope

- Key rotation or re-encryption of existing secrets
- Per-user secrets (all secrets are app-scoped)
- Secret versioning or audit log
- Azure Key Vault integration (future work; Data Protection infrastructure is already compatible)
