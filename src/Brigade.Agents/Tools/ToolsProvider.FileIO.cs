using Microsoft.Extensions.AI;

namespace Brigade.Agents.Tools;

public static partial class ToolsProvider
{
    public static AIFunction ReadAllLines { get; } = AIFunctionFactory.Create(
        (string filePath) => File.ReadAllTextAsync(filePath),
        name: "read-all-lines",
        description: "Reads all lines from a text file and returns them as a single string.");

    public static AIFunction WriteToFile { get; } = AIFunctionFactory.Create(
        (string filePath, string content) => File.WriteAllTextAsync(filePath, content),
        name: "write-to-file",
        description: "Writes the provided content to a text file at the specified path.");

    public static AIFunction GetFileSize { get; } = AIFunctionFactory.Create(
        (string filePath) => new FileInfo(filePath).Length,
        name: "get-file-size",
        description: "Returns the size of the specified file in bytes.");

    public static AIFunction CountLines { get; } = AIFunctionFactory.Create(
        (string filePath) => File.ReadLines(filePath).Count(),
        name: "count-lines",
        description: "Counts the number of lines in a text file and returns the count as an integer.");

    public static AIFunction AppendToFile { get; } = AIFunctionFactory.Create(
        (string filePath, string content) => File.AppendAllTextAsync(filePath, content),
        name: "append-to-file",
        description: "Appends the provided content to a text file at the specified path.");

    public static AIFunction GetLine { get; } = AIFunctionFactory.Create(
        async (string filePath, int lineNumber) =>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lineNumber > lines.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lineNumber), $"The file only has {lines.Length} lines.");
            }
            return lines[lineNumber - 1];
        },
        name: "get-line",
        description: "Returns the content of a specific line number from a text file.");

    public static AIFunction GetLines { get; } = AIFunctionFactory.Create(
        async (string filePath, int startLine, int endLine) =>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(startLine, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(endLine, startLine);
            var lines = await File.ReadAllLinesAsync(filePath);
            if (endLine > lines.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endLine), $"The file only has {lines.Length} lines.");
            }
            return string.Join(Environment.NewLine, lines[(startLine - 1)..endLine]);
        },
        name: "get-lines",
        description: "Returns the content of a range of line numbers from a text file, specified by start and end line numbers.");
}
