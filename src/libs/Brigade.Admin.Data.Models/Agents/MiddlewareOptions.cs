namespace Brigade.Admin.Data.Models.Agents;

public enum MiddlewareScope
{
    Agent,
    Run
}

public enum MiddlewareType
{
    BeforeExecution,
    AfterExecution,
    BeforeToolUse,
    AfterToolUse,
    InputGuardrail,
    OutputGuardrail,
    ErrorHandling
}

public record MiddlewareOptions
{
    public MiddlewareOptionsId Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public MiddlewareScope Scope { get; set; }
    public MiddlewareType Type { get; set; }
    public string? Content { get; set; }
    public List<AgentOptions> Agents { get; set; } = [];
}
