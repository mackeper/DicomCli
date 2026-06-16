using System.Text.Json;
using FellowOakDicom;

internal static class DicomwebJsonWriter
{
    private static readonly JsonSerializerOptions CompactOptions = new();
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    public static void Write(DicomDataset dataset, TextWriter output, bool compact)
    {
        output.WriteLine(JsonSerializer.Serialize(GetJsonDataset(dataset), compact ? CompactOptions : PrettyOptions));
    }

    private static SortedDictionary<string, object> GetJsonDataset(DicomDataset dataset)
    {
        return new SortedDictionary<string, object>(dataset.ToDictionary(
            item => GetTagString(item.Tag),
            item => GetJsonAttribute(item, dataset)));
    }

    private static object GetJsonAttribute(DicomItem item, DicomDataset dataset)
    {
        if (item is DicomSequence seq)
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetName(item),
                Value = seq.Items.Select(GetJsonDataset)
            };
        }

        if (item is DicomFragmentSequence frag)
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetName(item),
                InlineBinary = GetFragmentInlineBinary(frag)
            };
        }

        if (item is DicomElement elem && IsBinaryVR(elem.ValueRepresentation))
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetName(item),
                InlineBinary = GetInlineBinary(elem)
            };
        }

        return new
        {
            vr = item.ValueRepresentation.Code,
            name = GetName(item),
            Value = GetJsonValues(item, dataset)
        };
    }

    private static string GetTagString(DicomTag tag) => $"{tag.Group:X4}{tag.Element:X4}";

    private static string GetName(DicomItem item) => item.Tag.DictionaryEntry?.Name ?? item.Tag.ToString();

    private static object[] GetJsonValues(DicomItem item, DicomDataset dataset)
    {
        if (item.ValueRepresentation == DicomVR.PN)
        {
            return dataset.GetValues<string>(item.Tag).Select(GetPersonNameValue).ToArray();
        }

        if (item.ValueRepresentation == DicomVR.US)
        {
            return dataset.GetValues<ushort>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.SS)
        {
            return dataset.GetValues<short>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.UL)
        {
            return dataset.GetValues<uint>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.SL)
        {
            return dataset.GetValues<int>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.FL)
        {
            return dataset.GetValues<float>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.FD)
        {
            return dataset.GetValues<double>(item.Tag).Cast<object>().ToArray();
        }

        if (item.ValueRepresentation == DicomVR.AT)
        {
            return dataset.GetValues<DicomTag>(item.Tag).Select(tag => (object)GetTagString(tag)).ToArray();
        }

        return dataset.GetValues<string>(item.Tag).Cast<object>().ToArray();
    }

    private static object GetPersonNameValue(string value)
    {
        var components = value.Split('=', 3);
        var personName = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(components[0]))
        {
            personName["Alphabetic"] = components[0];
        }

        if (components.Length > 1 && !string.IsNullOrEmpty(components[1]))
        {
            personName["Ideographic"] = components[1];
        }

        if (components.Length > 2 && !string.IsNullOrEmpty(components[2]))
        {
            personName["Phonetic"] = components[2];
        }

        return personName;
    }

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
