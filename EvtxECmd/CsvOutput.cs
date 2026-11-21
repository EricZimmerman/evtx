using System;
using CsvHelper;

namespace EvtxECmd;

/// <summary>The CsvHelper 34 writer options that write what CsvHelper 33 wrote</summary>
internal static class CsvOutput
{
    /// <summary>
    ///     True/False, and fields quoted when they contain a comma, a quote, CR or LF, or start or end with a space (not a
    ///     tab: <see cref="CsvQuoting.WhenNeededOrWhitespace" /> also quotes those, which CsvHelper 33 did not). UTF-8
    ///     without a BOM; <c>Options with { WriteByteOrderMark = true }</c> for a BOM
    /// </summary>
    public static CsvWriterOptions Options { get; } = new()
    {
        TrueValue = "True",
        FalseValue = "False",
        ShouldQuote = QuoteLeadingOrTrailingSpace
    };

    private static bool QuoteLeadingOrTrailingSpace(ReadOnlySpan<char> field, Type fieldType, int fieldIndex, long recordIndex) =>
        field.Length > 0 && (field[0] == ' ' || field[field.Length - 1] == ' ');
}
