using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using FellowOakDicom;

public static class DicomCliApp
{
    public static int ExecuteWrite(string inputPath, string outputPath, TextWriter error)
    {
        if (!File.Exists(inputPath))
        {
            error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        try
        {
            using var stream = File.OpenRead(inputPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error.WriteLine("DICOMweb JSON root must be an object.");
                return 1;
            }

            var dataset = ReadDicomwebDataset(document.RootElement);
            var file = new DicomFile(dataset);
            file.Save(outputPath);
            return 0;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"Failed to parse JSON file: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to create DICOM file: {ex.Message}");
            return 1;
        }
        catch (FormatException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return 1;
        }
    }

    public static int ExecuteRead(string filePath, string format, string binaryFormat, TextWriter output, TextWriter error)
    {
        if (format == "json" && binaryFormat != "base64")
        {
            error.WriteLine($"--binary-format {binaryFormat} cannot be used with --format json. DICOMweb JSON requires base64 InlineBinary.");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(filePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return 1;
        }

        var dataset = file.Dataset;
        var transferSyntaxName = file.FileMetaInfo?.TransferSyntax?.UID?.Name ?? "Unknown";
        if (format == "json")
        {
            WriteJsonOutput(dataset, output);
            return 0;
        }

        output.WriteLine($"Transfer Syntax: {transferSyntaxName}");
        output.WriteLine();

        WriteTextDataset(dataset, 0, binaryFormat, output);

        return 0;
    }

    public static string ResolveBinaryFormatDefault(string format, string? binaryFormat)
    {
        return binaryFormat ?? (format == "json" ? "base64" : "summary");
    }

    private static DicomDataset ReadDicomwebDataset(JsonElement datasetElement)
    {
        var dataset = new DicomDataset();

        foreach (var tagProperty in datasetElement.EnumerateObject())
        {
            var tag = ParseDicomTag(tagProperty.Name);
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
            AddDicomwebAttribute(dataset, tag, vr, attribute);
        }

        return dataset;
    }

    private static DicomTag ParseDicomTag(string value)
    {
        if (value.Length != 8
            || !ushort.TryParse(value[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var group)
            || !ushort.TryParse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var element))
        {
            throw new FormatException($"Invalid DICOM tag '{value}'. Expected 8 hex characters.");
        }

        return new DicomTag(group, element);
    }

    private static void AddDicomwebAttribute(DicomDataset dataset, DicomTag tag, DicomVR vr, JsonElement attribute)
    {
        if (attribute.TryGetProperty("BulkDataURI", out _))
        {
            throw new FormatException($"Attribute {tag} uses unsupported BulkDataURI.");
        }

        if (vr == DicomVR.SQ)
        {
            dataset.Add(new DicomSequence(tag, ReadSequenceItems(tag, attribute)));
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

    private static DicomDataset[] ReadSequenceItems(DicomTag tag, JsonElement attribute)
    {
        if (!attribute.TryGetProperty("Value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<DicomDataset>();
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Sequence {tag} contains a non-object item.");
            }

            items.Add(ReadDicomwebDataset(item));
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
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => GetUInt16Value(tag, v)).ToArray());
            return;
        }

        if (vr == DicomVR.SS)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => GetInt16Value(tag, v)).ToArray());
            return;
        }

        if (vr == DicomVR.UL)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => v.GetUInt32()).ToArray());
            return;
        }

        if (vr == DicomVR.SL)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => v.GetInt32()).ToArray());
            return;
        }

        if (vr == DicomVR.FL)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => v.GetSingle()).ToArray());
            return;
        }

        if (vr == DicomVR.FD)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(v => v.GetDouble()).ToArray());
            return;
        }

        if (vr == DicomVR.AT)
        {
            dataset.AddOrUpdate(tag, values.EnumerateArray().Select(value => ParseDicomTag(GetDicomwebStringValue(tag, vr, value))).ToArray());
            return;
        }

        dataset.AddOrUpdate(vr, tag, values.EnumerateArray().Select(value => GetDicomwebStringValue(tag, vr, value)).ToArray());
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

    private static string GetDicomwebStringValue(DicomTag tag, DicomVR vr, JsonElement value)
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
        var alphabetic = GetOptionalStringProperty(value, "Alphabetic");
        var ideographic = GetOptionalStringProperty(value, "Ideographic");
        var phonetic = GetOptionalStringProperty(value, "Phonetic");

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

    private static string? GetOptionalStringProperty(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static void WriteJsonOutput(DicomDataset dataset, TextWriter output)
    {
        output.WriteLine(JsonSerializer.Serialize(GetDicomwebJsonDataset(dataset)));
    }

    private static SortedDictionary<string, object> GetDicomwebJsonDataset(DicomDataset dataset)
    {
        return new SortedDictionary<string, object>(dataset.ToDictionary(
            item => GetDicomwebTag(item.Tag),
            item => GetDicomwebJsonAttribute(item, dataset)));
    }

    private static object GetDicomwebJsonAttribute(DicomItem item, DicomDataset dataset)
    {
        if (item is DicomSequence seq)
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetDicomwebName(item),
                Value = seq.Items.Select(GetDicomwebJsonDataset)
            };
        }

        if (item is DicomFragmentSequence frag)
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetDicomwebName(item),
                InlineBinary = GetDicomwebFragmentInlineBinary(frag)
            };
        }

        if (item is DicomElement elem && IsBinaryVR(elem.ValueRepresentation))
        {
            return new
            {
                vr = item.ValueRepresentation.Code,
                name = GetDicomwebName(item),
                InlineBinary = GetDicomwebInlineBinary(elem)
            };
        }

        return new
        {
            vr = item.ValueRepresentation.Code,
            name = GetDicomwebName(item),
            Value = GetDicomwebJsonValues(item, dataset)
        };
    }

    private static string GetDicomwebTag(DicomTag tag) => $"{tag.Group:X4}{tag.Element:X4}";

    private static string GetDicomwebName(DicomItem item) => item.Tag.DictionaryEntry?.Name ?? item.Tag.ToString();

    private static object[] GetDicomwebJsonValues(DicomItem item, DicomDataset dataset)
    {
        if (item.ValueRepresentation == DicomVR.PN)
        {
            return dataset.GetValues<string>(item.Tag).Select(GetDicomwebPersonNameValue).ToArray();
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
            return dataset.GetValues<DicomTag>(item.Tag).Select(tag => (object)GetDicomwebTag(tag)).ToArray();
        }

        return dataset.GetValues<string>(item.Tag).Cast<object>().ToArray();
    }

    private static object GetDicomwebPersonNameValue(string value)
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

    private static string GetDicomwebInlineBinary(DicomElement elem)
    {
        return Convert.ToBase64String(elem.Buffer?.Data ?? Array.Empty<byte>());
    }

    private static string GetDicomwebFragmentInlineBinary(DicomFragmentSequence frag)
    {
        return Convert.ToBase64String(frag.Fragments.SelectMany(b => b?.Data ?? Array.Empty<byte>()).ToArray());
    }

    private static void WriteTextDataset(DicomDataset dataset, int indent, string binaryFormat, TextWriter output)
    {
        foreach (var item in dataset)
        {
            var padding = new string(' ', indent);
            var tagStr = item.ToString();
            var valueStr = GetTextValueString(item, dataset, binaryFormat);
            output.WriteLine($"{padding}{tagStr} = {valueStr}");

            if (item is DicomSequence sequence)
            {
                WriteTextSequence(sequence, indent + 2, binaryFormat, output);
            }
        }
    }

    private static void WriteTextSequence(DicomSequence sequence, int indent, string binaryFormat, TextWriter output)
    {
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var padding = new string(' ', indent);
            output.WriteLine($"{padding}Item {i + 1}:");
            WriteTextDataset(sequence.Items[i], indent + 2, binaryFormat, output);
        }
    }

    private static string GetTextValueString(DicomItem item, DicomDataset dataset, string binaryFormat)
    {
        return item switch
        {
            DicomSequence seq => $"[{seq.Items.Count} items]",
            DicomFragmentSequence frag => GetTextFragmentValueString(frag, binaryFormat),
            DicomElement elem => GetElementValueString(elem, dataset, binaryFormat),
            _ => "[unknown]"
        };
    }

    private static string GetTextFragmentValueString(DicomFragmentSequence frag, string binaryFormat)
    {
        return binaryFormat switch
        {
            "base64" => GetDicomwebFragmentInlineBinary(frag),
            "hex" => $"[{string.Join(", ", frag.Fragments.Select(b => Convert.ToHexString(b?.Data ?? Array.Empty<byte>())))}]",
            _ => GetBinarySummary(frag.Fragments.Sum(b => b?.Size ?? 0))
        };
    }

    private static string GetElementValueString(DicomElement elem, DicomDataset dataset, string binaryFormat)
    {
        if (IsBinaryVR(elem.ValueRepresentation))
        {
            return GetBinaryValueString(elem, binaryFormat);
        }

        return dataset.TryGetString(elem.Tag, out var s) && !string.IsNullOrEmpty(s)
            ? s
            : "[empty]";
    }

    private static string GetBinaryValueString(DicomElement elem, string binaryFormat)
    {
        return binaryFormat switch
        {
            "base64" => GetDicomwebInlineBinary(elem),
            "hex" => Convert.ToHexString(elem.Buffer?.Data ?? Array.Empty<byte>()),
            _ => GetBinarySummary(elem.Buffer?.Size ?? 0)
        };
    }

    private static string GetBinarySummary(long size) => $"[{size} bytes]";

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
