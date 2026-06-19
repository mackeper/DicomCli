using System.Security.Cryptography;
using FellowOakDicom;

internal static class DicomDatasetComparer
{
    public static IReadOnlyList<DicomCompareDifference> Compare(DicomDataset left, DicomDataset right)
    {
        var leftEntries = Flatten(left);
        var rightEntries = Flatten(right);
        var paths = leftEntries.Keys.Concat(rightEntries.Keys).Distinct().Order(StringComparer.Ordinal);
        var differences = new List<DicomCompareDifference>();

        foreach (var path in paths)
        {
            var hasLeft = leftEntries.TryGetValue(path, out var leftEntry);
            var hasRight = rightEntries.TryGetValue(path, out var rightEntry);

            if (hasLeft && hasRight)
            {
                if (leftEntry!.Header != rightEntry!.Header || leftEntry.ComparisonValue != rightEntry.ComparisonValue)
                {
                    differences.Add(new ChangedDicomCompareDifference(path, leftEntry.ToSide(), rightEntry.ToSide()));
                }

                continue;
            }

            if (hasRight)
            {
                differences.Add(new AddedDicomCompareDifference(path, rightEntry!.ToSide()));
            }
            else
            {
                differences.Add(new RemovedDicomCompareDifference(path, leftEntry!.ToSide()));
            }
        }

        return differences;
    }

    public static void WriteDifferences(IEnumerable<DicomCompareDifference> differences, TextWriter output, bool color = false)
    {
        foreach (var difference in differences)
        {
            switch (difference)
            {
                case ChangedDicomCompareDifference changed:
                    var leftHeader = GetHeader(changed.Path, changed.Left);
                    var rightHeader = GetHeader(changed.Path, changed.Right);
                    output.WriteLine($"{AnsiColor.Colorize("~", AnsiColor.Yellow, color)} {leftHeader}");
                    if (leftHeader != rightHeader)
                    {
                        output.WriteLine($"  right header: {rightHeader}");
                    }

                    output.WriteLine($"  left:  {changed.Left.DisplayValue}");
                    output.WriteLine($"  right: {changed.Right.DisplayValue}");
                    break;

                case AddedDicomCompareDifference added:
                    output.WriteLine($"{AnsiColor.Colorize("+", AnsiColor.Green, color)} {GetHeader(added.Path, added.Right)}");
                    output.WriteLine($"  right: {added.Right.DisplayValue}");
                    break;

                case RemovedDicomCompareDifference removed:
                    output.WriteLine($"{AnsiColor.Colorize("-", AnsiColor.Red, color)} {GetHeader(removed.Path, removed.Left)}");
                    output.WriteLine($"  left:  {removed.Left.DisplayValue}");
                    break;
            }
        }
    }

    private static SortedDictionary<string, CompareEntry> Flatten(DicomDataset dataset)
    {
        var entries = new SortedDictionary<string, CompareEntry>(StringComparer.Ordinal);
        AddDatasetEntries(dataset, string.Empty, entries);
        return entries;
    }

    private static void AddDatasetEntries(DicomDataset dataset, string prefix, SortedDictionary<string, CompareEntry> entries)
    {
        foreach (var item in dataset)
        {
            var path = string.IsNullOrEmpty(prefix) ? GetTagString(item.Tag) : $"{prefix}.{GetTagString(item.Tag)}";
            var value = GetValueSummary(item, dataset);
            entries[path] = new CompareEntry(path, item.ValueRepresentation.Code, GetName(item), value.Display, value.Comparison);

            if (item is DicomSequence sequence)
            {
                for (var i = 0; i < sequence.Items.Count; i++)
                {
                    AddDatasetEntries(sequence.Items[i], $"{path}[{i + 1}]", entries);
                }
            }
        }
    }

    private static ValueSummary GetValueSummary(DicomItem item, DicomDataset dataset)
    {
        return item switch
        {
            DicomSequence sequence => CreateTextSummary($"[{sequence.Items.Count} items]"),
            DicomFragmentSequence fragmentSequence => GetFragmentBinarySummary(fragmentSequence),
            DicomElement element when IsBinaryVR(element.ValueRepresentation) => GetBinarySummary(element.Buffer?.Data ?? Array.Empty<byte>()),
            DicomElement element => GetElementValueSummary(element, dataset),
            _ => CreateTextSummary("[unknown]")
        };
    }

    private static ValueSummary GetElementValueSummary(DicomElement element, DicomDataset dataset)
    {
        if (!dataset.TryGetString(element.Tag, out var value) || value.Length == 0)
        {
            return new ValueSummary("\"\"", string.Empty);
        }

        return CreateTextSummary(value);
    }

    private static ValueSummary GetFragmentBinarySummary(DicomFragmentSequence fragmentSequence)
    {
        using var sha256 = SHA256.Create();
        var length = 0L;
        foreach (var fragment in fragmentSequence.Fragments)
        {
            var data = fragment?.Data ?? Array.Empty<byte>();
            length += data.Length;
            sha256.TransformBlock(data, 0, data.Length, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return CreateBinarySummary(length, Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant());
    }

    private static ValueSummary GetBinarySummary(byte[] data)
    {
        return CreateBinarySummary(data.Length, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());
    }

    private static ValueSummary CreateBinarySummary(long length, string hash)
    {
        return new ValueSummary($"[{length} bytes, sha256:{hash[..16]}]", $"[{length} bytes, sha256:{hash}]");
    }

    private static ValueSummary CreateTextSummary(string value) => new(value, value);

    private static string GetTagString(DicomTag tag) => $"{tag.Group:X4}{tag.Element:X4}";

    private static string GetName(DicomItem item) => item.Tag.DictionaryEntry?.Name ?? item.Tag.ToString();

    private static string GetHeader(string path, DicomCompareSide side) => $"{path} {side.ValueRepresentation} {side.Name}";

    private static bool IsBinaryVR(DicomVR vr)
    {
        return vr == DicomVR.OB
            || vr == DicomVR.OW
            || vr == DicomVR.OD
            || vr == DicomVR.OF
            || vr == DicomVR.OL
            || vr == DicomVR.OV
            || vr == DicomVR.UN;
    }

    private sealed record CompareEntry(string Path, string ValueRepresentation, string Name, string DisplayValue, string ComparisonValue)
    {
        public string Header => $"{Path} {ValueRepresentation} {Name}";

        public DicomCompareSide ToSide() => new(ValueRepresentation, Name, DisplayValue, ComparisonValue);
    }

    private sealed record ValueSummary(string Display, string Comparison);
}

internal abstract record DicomCompareDifference(string Path);

internal sealed record AddedDicomCompareDifference(string Path, DicomCompareSide Right) : DicomCompareDifference(Path);

internal sealed record RemovedDicomCompareDifference(string Path, DicomCompareSide Left) : DicomCompareDifference(Path);

internal sealed record ChangedDicomCompareDifference(string Path, DicomCompareSide Left, DicomCompareSide Right) : DicomCompareDifference(Path);

internal sealed record DicomCompareSide(string ValueRepresentation, string Name, string DisplayValue, string ComparisonValue);
