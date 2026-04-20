using Microsoft.Extensions.AI;

namespace Brigade.Agents.Tools;

public static partial class ToolsProvider
{
    public static AITool GetTool(string toolName) => toolName switch
    {
        "read-all-lines" => ReadAllLines,
        "write-to-file"  => WriteToFile,
        "get-file-size"  => GetFileSize,
        "count-lines"    => CountLines,
        "append-to-file" => AppendToFile,
        "get-line"       => GetLine,
        "get-lines"      => GetLines,
        _ => throw new NotSupportedException($"The tool {toolName} is not supported."),
    };
}
