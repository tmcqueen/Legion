---
name: Secrets Management Feature
description: Design and implementation status for the secrets management feature on branch features/secrets-management
type: project
originSessionId: f4610344-540b-4116-abe2-fb6739ee67fd
---
Brainstorming session completed (2026-04-27). Design spec approved and committed to `docs/2026-04-27-secrets-management-design.md` on branch `features/secrets-management` (worktree: `/home/timm/Legion/.worktrees/features/secrets-management`).

**Why:** Providers and MCP servers currently store sensitive values (API tokens, header values, command lines) as plain strings. The feature introduces a central, path-addressable secrets registry so resources hold URIs instead of raw values.

**How to apply:** Next step is to invoke the `superpowers:writing-plans` skill to produce the implementation plan. The design is fully approved — do not re-brainstorm.

## Key design decisions

- **Convention-based (Option B):** Existing model fields (`ProviderOptions.ApiToken`, `McpServerHeaders.Value`, `McpServerOptions.CommandLine`) are unchanged. They store either a raw value or a `secret://` URI string. No FK columns added.
- **Path format:** `secret://openai/client_ids/test` — stored as `openai/client_ids/test` (scheme stripped) in the `SecretOptions.Path` column.
- **New table only:** `SecretOptions` (`Id`, `Path`, `Description`, `EncryptedValue`, `CreatedAt`, `UpdatedAt`). Migration adds this table; no existing tables touched.
- **Encryption:** PostgreSQL → pgcrypto (`pgp_sym_encrypt` / `pgp_sym_decrypt`), passphrase from config key `Secrets:EncryptionKey`, stored as `bytea`. SQLite → plaintext `text` (dev/test only).
- **Provider-specific stores:** `PostgreSqlSecretsStore` (raw SQL) in `Legion.Admin.Data.PostgreSQL`; `SqliteSecretsStore` (EF Core) in `Legion.Admin.Data.Sqlite`. Both implement `ISecretsStore`.
- **`ISecretsManager`:** Scoped service. `ResolveAsync(SecretRequest)` supports `text/plain` and `text/json` media types. Leaf vs collection resolved by exact-match vs prefix query (direct children only).
- **`SecretResolvingHandler`:** Transient `DelegatingHandler` for outbound `HttpClient` pipelines. Scans request headers for `secret://` and resolves before sending. Added to `AgentFactory` HTTP clients in `Legion.Agents`.
- **`Legion.Agents`** gains a project reference to `Legion.Admin.Data` for `ISecretsManager`.
- **UI:** `RadzenTree` split-panel page at `/admin/secrets`. Tree built client-side from flat `SecretOptions` list. Filter via `RadzenTextBox` + `@oninput`. Context menu per node type. Reveal button calls `POST /api/secrets/{id}/reveal` (admin-only), shows value 30s then re-masks. Provider/MCP Server forms get a raw/secret toggle on sensitive fields.
