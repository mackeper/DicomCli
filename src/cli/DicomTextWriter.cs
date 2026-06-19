using FellowOakDicom;

internal static class DicomTextWriter
{
    public static void Write(DicomDataset dataset, string transferSyntaxName, BinaryFormat binaryFormat, TextWriter output, bool color = false)
    {
        output.WriteLine($"{AnsiColor.Colorize("Transfer Syntax", AnsiColor.Cyan, color)}: {transferSyntaxName}");
        output.WriteLine();
        WriteDataset(dataset, 0, binaryFormat, output, color);
    }

    private static void WriteDataset(DicomDataset dataset, int indent, BinaryFormat binaryFormat, TextWriter output, bool color)
    {
        foreach (var item in dataset)
        {
            var padding = new string(' ', indent);
            var tagStr = item.ToString();
            var valueStr = GetValueString(item, dataset, binaryFormat);
            output.WriteLine($"{padding}{AnsiColor.Colorize(tagStr, AnsiColor.Cyan, color)} = {valueStr}");

            if (item is DicomSequence sequence)
            {
                WriteSequence(sequence, indent + 2, binaryFormat, output, color);
            }
        }
    }

    private static void WriteSequence(DicomSequence sequence, int indent, BinaryFormat binaryFormat, TextWriter output, bool color)
    {
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var padding = new string(' ', indent);
            output.WriteLine($"{padding}{AnsiColor.Colorize($"Item {i + 1}", AnsiColor.Yellow, color)}:");
            WriteDataset(sequence.Items[i], indent + 2, binaryFormat, output, color);
        }
    }

    private static string GetValueString(DicomItem item, DicomDataset dataset, BinaryFormat binaryFormat)
    {
        return item switch
        {
            DicomSequence seq => $"[{seq.Items.Count} items]",
            DicomFragmentSequence frag => GetFragmentValueString(frag, binaryFormat),
            DicomElement elem => GetElementValueString(elem, dataset, binaryFormat),
            _ => "[unknown]"
        };
    }

    private static string GetFragmentValueString(DicomFragmentSequence frag, BinaryFormat binaryFormat)
    {
        return binaryFormat switch
        {
            BinaryFormat.Base64 => GetFragmentInlineBinary(frag),
            BinaryFormat.Hex => $"[{string.Join(", ", frag.Fragments.Select(b => Convert.ToHexString(b?.Data ?? Array.Empty<byte>())))}]",
            _ => GetBinarySummary(frag.Fragments.Sum(b => b?.Size ?? 0))
        };
    }

    private static string GetElementValueString(DicomElement elem, DicomDataset dataset, BinaryFormat binaryFormat)
    {
        if (IsBinaryVR(elem.ValueRepresentation))
        {
            return GetBinaryValueString(elem, binaryFormat);
        }

        return dataset.TryGetString(elem.Tag, out var s) && !string.IsNullOrEmpty(s)
            ? s
            : "[empty]";
    }

    private static string GetBinaryValueString(DicomElement elem, BinaryFormat binaryFormat)
    {
        return binaryFormat switch
        {
            BinaryFormat.Base64 => GetInlineBinary(elem),
            BinaryFormat.Hex => Convert.ToHexString(elem.Buffer?.Data ?? Array.Empty<byte>()),
            _ => GetBinarySummary(elem.Buffer?.Size ?? 0)
        };
    }

    private static string GetBinarySummary(long size) => $"[{size} bytes]";

    private static string GetInlineBinary(DicomElement elem)
    {
        return Convert.ToBase64String(elem.Buffer?.Data ?? Array.Empty<byte>());
    }

    private static string GetFragmentInlineBinary(DicomFragmentSequence frag)
    {
        return Convert.ToBase64String(frag.Fragments.SelectMany(b => b?.Data ?? Array.Empty<byte>()).ToArray());
    }

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
}
