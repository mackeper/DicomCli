using System.Security.Cryptography;
using FellowOakDicom;

internal static class DicomDatasetComparer
{
    public static void WriteDifferences(DicomDataset left, DicomDataset right, TextWriter output)
    {
        var leftEntries = Flatten(left);
        var rightEntries = Flatten(right);
        var paths = leftEntries.Keys.Concat(rightEntries.Keys).Distinct().Order(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var hasLeft = leftEntries.TryGetValue(path, out var leftEntry);
            var hasRight = rightEntries.TryGetValue(path, out var rightEntry);

            if (hasLeft && hasRight)
            {
                if (leftEntry!.Header != rightEntry!.Header || leftEntry.ComparisonValue != rightEntry.ComparisonValue)
                {
                    output.WriteLine($"~ {leftEntry.Header}");
                    if (leftEntry.Header != rightEntry.Header)
                    {
                        output.WriteLine($"  right header: {rightEntry.Header}");
                    }

                    output.WriteLine($"  left:  {leftEntry.DisplayValue}");
                    output.WriteLine($"  right: {rightEntry.DisplayValue}");
                }

                continue;
            }

            if (hasRight)
            {
                output.WriteLine($"+ {rightEntry!.Header}");
                output.WriteLine($"  right: {rightEntry.DisplayValue}");
            }
            else
            {
                output.WriteLine($"- {leftEntry!.Header}");
                output.WriteLine($"  left:  {leftEntry.DisplayValue}");
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
    }

    private sealed record ValueSummary(string Display, string Comparison);
}
