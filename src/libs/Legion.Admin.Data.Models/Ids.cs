namespace Legion.Admin.Data.Models;

public readonly record struct AgentOptionsId(Guid Value)
{
    public static AgentOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(AgentOptionsId id) => id.Value;
    public static implicit operator AgentOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct McpServerOptionsId(Guid Value)
{
    public static McpServerOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(McpServerOptionsId id) => id.Value;
    public static implicit operator McpServerOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct McpServerHeadersId(Guid Value)
{
    public static McpServerHeadersId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(McpServerHeadersId id) => id.Value;
    public static implicit operator McpServerHeadersId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct MemoryOptionsId(Guid Value)
{
    public static MemoryOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MemoryOptionsId id) => id.Value;
    public static implicit operator MemoryOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct MiddlewareOptionsId(Guid Value)
{
    public static MiddlewareOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MiddlewareOptionsId id) => id.Value;
    public static implicit operator MiddlewareOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ModelOptionsId(Guid Value)
{
    public static ModelOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ModelOptionsId id) => id.Value;
    public static implicit operator ModelOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ProviderOptionsId(Guid Value)
{
    public static ProviderOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProviderOptionsId id) => id.Value;
    public static implicit operator ProviderOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SkillOptionsId(Guid Value)
{
    public static SkillOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(SkillOptionsId id) => id.Value;
    public static implicit operator SkillOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ToolOptionsId(Guid Value)
{
    public static ToolOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ToolOptionsId id) => id.Value;
    public static implicit operator ToolOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct WorkflowOptionsId(Guid Value)
{
    public static WorkflowOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(WorkflowOptionsId id) => id.Value;
    public static implicit operator WorkflowOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SecretOptionsId(Guid Value)
{
    public static SecretOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(SecretOptionsId id) => id.Value;
    public static implicit operator SecretOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
