
namespace BinOcular.Api.Models;

public sealed class FormattedOutput
{
    public required string Title { get; init; }

    public List<OutputSection> Sections { get; init; } = [];
}

public sealed class OutputSection
{
    public required string Title { get; init; }

    public required string Type { get; init; }

    public List<OutputField> Fields { get; init; } = [];

    public List<string> Columns { get; init; } = [];

    public List<List<string>> Rows { get; init; } = [];

    public string? Text { get; init; }
}

public sealed class OutputField
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public string? ValueType { get; init; }
}
