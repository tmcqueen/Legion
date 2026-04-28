using Brigade.Admin.Data.Services;

namespace Brigade.Agents.Http;

public class SecretResolvingHandler(ISecretsManager secrets) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var (key, values) in request.Headers.ToList())
        {
            if (!values.Any(v => v.Contains("secret://", StringComparison.OrdinalIgnoreCase)))
                continue;

            var newValues = new List<string>();
            foreach (var v in values)
            {
                var tokens = v.Split(' ');
                var newTokens = new List<string>();
                foreach (var token in tokens)
                {
                    newTokens.Add(secrets.IsSecretReference(token)
                        ? await secrets.ResolveAsync(new SecretRequest { Path = token }, ct) ?? token
                        : token);
                }
                newValues.Add(string.Join(" ", newTokens));
            }
            request.Headers.Remove(key);
            request.Headers.TryAddWithoutValidation(key, newValues);
        }
        return await base.SendAsync(request, ct);
    }
}
