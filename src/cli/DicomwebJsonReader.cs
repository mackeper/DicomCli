using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using FellowOakDicom;

internal static class DicomwebJsonReader
{
    public static DicomDataset Read(JsonElement datasetElement, bool validateItems = true)
    {
        var dataset = new DicomDataset();
        if (!validateItems)
        {
            dataset.NotValidated();
        }

        var tagProperties = datasetElement.EnumerateObject().ToArray();

        foreach (var tagProperty in tagProperties.Where(IsPrivateCreatorAttribute))
        {
            AddJsonAttribute(dataset, tagProperty, validateItems);
        }

        foreach (var tagProperty in tagProperties.Where(tagProperty => !IsPrivateCreatorAttribute(tagProperty)))
        {
            AddJsonAttribute(dataset, tagProperty, validateItems);
        }

        return dataset;
    }

    private static void AddJsonAttribute(DicomDataset dataset, JsonProperty tagProperty, bool validateItems)
    {
        var tag = ParseTag(tagProperty.Name);
        var attribute = tagProperty.Value;
        if (attribute.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"Attribute {tagProperty.Name} must be an object.");
        }

        if (!attribute.TryGetProperty("vr", out var vrElement) || vrElement.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"Attribute {tagProperty.Name} must contain string property 'vr'.");
        }

        var vr = DicomVR.Parse(vrElement.GetString() ?? string.Empty);
        tag = ResolvePrivateTag(dataset, tag);
        AddAttribute(dataset, tag, vr, attribute, validateItems);
    }

    private static bool IsPrivateCreatorAttribute(JsonProperty tagProperty)
    {
        var tag = ParseTag(tagProperty.Name);
        return tag.Group % 2 != 0 && tag.Element is >= 0x0010 and <= 0x00ff;
    }

    private static DicomTag ParseTag(string value)
    {
        if (value.Length != 8
            || !ushort.TryParse(value[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var group)
            || !ushort.TryParse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var element))
        {
            throw new FormatException($"Invalid DICOM tag '{value}'. Expected 8 hex characters.");
        }

        return new DicomTag(group, element);
    }

    private static DicomTag ResolvePrivateTag(DicomDataset dataset, DicomTag tag)
    {
        if (tag.Group % 2 == 0 || tag.Element < 0x1000)
        {
            return tag;
        }

        var creatorElement = (ushort)(tag.Element >> 8);
        if (creatorElement < 0x10 || creatorElement > 0xff)
        {
            return tag;
        }

        var creatorTag = new DicomTag(tag.Group, creatorElement);
        return dataset.TryGetSingleValue<string>(creatorTag, out var creator) && !string.IsNullOrWhiteSpace(creator)
            ? new DicomTag(tag.Group, tag.Element, new DicomPrivateCreator(creator))
            : tag;
    }

    private static void AddAttribute(DicomDataset dataset, DicomTag tag, DicomVR vr, JsonElement attribute, bool validateItems)
    {
        if (attribute.TryGetProperty("BulkDataURI", out _))
        {
            throw new FormatException($"Attribute {tag} uses unsupported BulkDataURI.");
        }

        if (vr == DicomVR.SQ)
        {
            dataset.Add(new DicomSequence(tag, ReadSequenceItems(tag, attribute, validateItems)));
            return;
        }

        if (attribute.TryGetProperty("InlineBinary", out var inlineBinary))
        {
            AddBinaryAttribute(dataset, tag, vr, inlineBinary);
            return;
        }

        if (!attribute.TryGetProperty("Value", out var values))
        {
            dataset.AddOrUpdate(vr, tag, Array.Empty<string>());
            return;
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"Value for {tag} must be an array.");
        }

        AddValueAttribute(dataset, tag, vr, values);
    }

    private static DicomDataset[] ReadSequenceItems(DicomTag tag, JsonElement attribute, bool validateItems)
    {
        if (!attribute.TryGetProperty("Value", out var values))
        {
            return [];
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"Value for {tag} must be an array.");
        }

        var items = new List<DicomDataset>();
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Sequence {tag} contains a non-object item.");
            }

            items.Add(Read(item, validateItems));
        }

        return [.. items];
    }

    private static void AddBinaryAttribute(DicomDataset dataset, DicomTag tag, DicomVR vr, JsonElement inlineBinary)
    {
        if (inlineBinary.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"InlineBinary for {tag} must be a base64 string.");
        }

        var bytes = Convert.FromBase64String(inlineBinary.GetString() ?? string.Empty);
        if (vr == DicomVR.OB)
        {
            dataset.Add(new DicomOtherByte(tag, bytes));
            return;
        }

        if (vr == DicomVR.OW)
        {
            dataset.Add(new DicomOtherWord(tag, GetLittleEndianWords(bytes)));
            return;
        }

        if (vr == DicomVR.OF)
        {
            dataset.Add(new DicomOtherFloat(tag, GetLittleEndianSingles(bytes)));
            return;
        }

        if (vr == DicomVR.OD)
        {
            dataset.Add(new DicomOtherDouble(tag, GetLittleEndianDoubles(bytes)));
            return;
        }

        if (vr == DicomVR.OL)
        {
            dataset.Add(new DicomOtherLong(tag, GetLittleEndianUInt32s(bytes)));
            return;
        }

        if (vr == DicomVR.OV)
        {
            dataset.Add(new DicomOtherVeryLong(tag, GetLittleEndianUInt64s(bytes)));
            return;
        }

        if (vr == DicomVR.UN)
        {
            dataset.Add(new DicomUnknown(tag, bytes));
            return;
        }

        throw new FormatException($"InlineBinary is not supported for VR {vr.Code}.");
    }

    private static ushort[] GetLittleEndianWords(byte[] bytes)
    {
        RequireByteMultiple(bytes, sizeof(ushort), DicomVR.OW);

        var words = new ushort[(bytes.Length + 1) / 2];
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * sizeof(ushort), sizeof(ushort)));
        }

        return words;
    }

    private static float[] GetLittleEndianSingles(byte[] bytes)
    {
        RequireByteMultiple(bytes, sizeof(float), DicomVR.OF);

        var values = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < values.Length; i++)
        {
            var bits = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)));
            values[i] = BitConverter.Int32BitsToSingle(bits);
        }

        return values;
    }

    private static double[] GetLittleEndianDoubles(byte[] bytes)
    {
        RequireByteMultiple(bytes, sizeof(double), DicomVR.OD);

        var values = new double[bytes.Length / sizeof(double)];
        for (var i = 0; i < values.Length; i++)
        {
            var bits = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(i * sizeof(double), sizeof(double)));
            values[i] = BitConverter.Int64BitsToDouble(bits);
        }

        return values;
    }

    private static uint[] GetLittleEndianUInt32s(byte[] bytes)
    {
        RequireByteMultiple(bytes, sizeof(uint), DicomVR.OL);

        var values = new uint[bytes.Length / sizeof(uint)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)));
        }

        return values;
    }

    private static ulong[] GetLittleEndianUInt64s(byte[] bytes)
    {
        RequireByteMultiple(bytes, sizeof(ulong), DicomVR.OV);

        var values = new ulong[bytes.Length / sizeof(ulong)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(i * sizeof(ulong), sizeof(ulong)));
        }

        return values;
    }

    private static void RequireByteMultiple(byte[] bytes, int valueSize, DicomVR vr)
    {
        if (bytes.Length % valueSize != 0)
        {
            throw new FormatException($"InlineBinary length for VR {vr.Code} must be a multiple of {valueSize} bytes.");
        }
    }

    private static void AddValueAttribute(DicomDataset dataset, DicomTag tag, DicomVR vr, JsonElement values)
    {
        if (vr == DicomVR.US)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => GetUInt16Value(tag, v)).ToArray());
            return;
        }

        if (vr == DicomVR.SS)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => GetInt16Value(tag, v)).ToArray());
            return;
        }

        if (vr == DicomVR.UL)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => v.GetUInt32()).ToArray());
            return;
        }

        if (vr == DicomVR.SL)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => v.GetInt32()).ToArray());
            return;
        }

        if (vr == DicomVR.FL)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => v.GetSingle()).ToArray());
            return;
        }

        if (vr == DicomVR.FD)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(v => v.GetDouble()).ToArray());
            return;
        }

        if (vr == DicomVR.AT)
        {
            dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(value => ParseTag(GetStringValue(tag, vr, value))).ToArray());
            return;
        }

        dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(value => GetStringValue(tag, vr, value)).ToArray());
    }

    private static ushort GetUInt16Value(DicomTag tag, JsonElement value)
    {
        var number = value.GetUInt32();
        if (number > ushort.MaxValue)
        {
            throw new FormatException($"Value {number} for {tag} exceeds US maximum {ushort.MaxValue}.");
        }

        return (ushort)number;
    }

    private static short GetInt16Value(DicomTag tag, JsonElement value)
    {
        var number = value.GetInt32();
        if (number < short.MinValue || number > short.MaxValue)
        {
            throw new FormatException($"Value {number} for {tag} is outside SS range {short.MinValue} to {short.MaxValue}.");
        }

        return (short)number;
    }

    private static string GetStringValue(DicomTag tag, DicomVR vr, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (vr != DicomVR.PN)
            {
                throw new FormatException($"Object value for {tag} is only supported for PN VR.");
            }

            return GetPersonNameValue(value);
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static string GetPersonNameValue(JsonElement value)
    {
        var alphabetic = GetOptionalString(value, "Alphabetic");
        var ideographic = GetOptionalString(value, "Ideographic");
        var phonetic = GetOptionalString(value, "Phonetic");

        if (phonetic is not null)
        {
            return $"{alphabetic ?? string.Empty}={ideographic ?? string.Empty}={phonetic}";
        }

        if (ideographic is not null)
        {
            return $"{alphabetic ?? string.Empty}={ideographic}";
        }

        return alphabetic ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
