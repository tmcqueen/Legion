namespace Brigade.Admin.Data.Models.Agents;

public enum TransportType
{
    StdinOut,
    Http,
    WebSocket,
    Grpc
}

public record McpServerHeaders
{
    public int Id { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public int McpServerId { get; set; }
    public McpServerOptions? McpServer { get; set; }
}

public record McpServerOptions
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ServerUrl { get; set; }
    public string? ServerLabel { get; set; }
    public TransportType Transport { get; set; }
    public bool RequireApproval { get; set; }
    public string? CommandLine { get; set; }
    public List<AgentOptions> Agents { get; set; } = [];
    public List<McpServerHeaders> Headers { get; set; } = [];
}
