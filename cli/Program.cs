using System.Text;

string filePath = args.Length > 0 ? args[0] : "0002.DCM";

if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return 1;
}

byte[] data = File.ReadAllBytes(filePath);
int pos = 0;
int length = data.Length;

// Check for DICOM preamble
bool hasPreamble = false;
if (length >= 132)
{
    string dicm = Encoding.ASCII.GetString(data, 128, 4);
    if (dicm == "DICM")
    {
        hasPreamble = true;
        pos = 132;
    }
}

// Meta-info group (0002) is always Explicit VR Little Endian
bool explicitVr = true;
string transferSyntax = "1.2.840.10008.1.2"; // default Implicit VR Little Endian

while (pos < length)
{
    // Need at least 4 bytes for tag
    if (pos + 4 > length) break;

    ushort group = ReadU16(data, pos);
    ushort element = ReadU16(data, pos + 2);
    pos += 4;

    // Meta-info group (0002) is always Explicit VR Little Endian
    // Switch to dataset transfer syntax only for groups > 0002
    if (hasPreamble && group > 0x0002)
    {
        // Only Implicit VR Little Endian (1.2.840.10008.1.2) uses implicit VR.
        // All other transfer syntaxes are explicit VR (mostly little endian).
        explicitVr = transferSyntax != "1.2.840.10008.1.2";
    }

    // Check for sequence/item delimiters which have no VR and 4-byte length
    bool isDelimiter = (group == 0xFFFE && (element == 0xE000 || element == 0xE00D || element == 0xE0DD));

    string vr = "";
    uint valueLength = 0;

    if (isDelimiter)
    {
        if (pos + 4 > length) break;
        valueLength = ReadU32(data, pos);
        pos += 4;
    }
    else if (explicitVr)
    {
        if (pos + 2 > length) break;
        vr = Encoding.ASCII.GetString(data, pos, 2);
        pos += 2;

        // Long-form VRs: OB, OD, OF, OW, SQ, UC, UN, UT
        if (vr == "OB" || vr == "OD" || vr == "OF" || vr == "OW" ||
            vr == "SQ" || vr == "UC" || vr == "UN" || vr == "UT")
        {
            if (pos + 4 > length) break;
            pos += 2; // reserved
            valueLength = ReadU32(data, pos);
            pos += 4;
        }
        else
        {
            if (pos + 2 > length) break;
            valueLength = ReadU16(data, pos);
            pos += 2;
        }
    }
    else // Implicit VR
    {
        if (pos + 4 > length) break;
        valueLength = ReadU32(data, pos);
        pos += 4;
        // Try to infer VR for display
        vr = InferImplicitVr(group, element);
    }

    // Undefined length for SQ or items
    if (valueLength == 0xFFFFFFFF)
    {
        Console.WriteLine($"({group:X4},{element:X4}) {vr} [undefined length]");
        continue;
    }

    if (pos + valueLength > length) break;

    // Read value preview
    string valuePreview = GetValuePreview(data, pos, (int)valueLength, vr, group, element);

    Console.WriteLine($"({group:X4},{element:X4}) {vr} {valuePreview}");

    // Special handling for Transfer Syntax UID
    if (group == 0x0002 && element == 0x0010 && valueLength > 0)
    {
        transferSyntax = Encoding.ASCII.GetString(data, pos, (int)valueLength).TrimEnd('\0');
    }

    pos += (int)valueLength;
}

return 0;

static ushort ReadU16(byte[] data, int offset)
{
    return (ushort)(data[offset] | (data[offset + 1] << 8));
}

static uint ReadU32(byte[] data, int offset)
{
    return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}

static string InferImplicitVr(ushort group, ushort element)
{
    // Minimal inference for common tags
    if (group == 0x0008 || group == 0x0010 || group == 0x0018 || group == 0x0020 ||
        group == 0x0028 || group == 0x0032 || group == 0x0040)
    {
        return "?";
    }
    return "?";
}

static string GetValuePreview(byte[] data, int offset, int len, string vr, ushort group, ushort element)
{
    if (len == 0) return "(empty)";

    // For large values, just show length
    if (len > 64)
    {
        return $"[{len} bytes]";
    }

    // For text VRs
    if (vr == "AE" || vr == "AS" || vr == "AT" || vr == "CS" || vr == "DA" ||
        vr == "DS" || vr == "DT" || vr == "IS" || vr == "LO" || vr == "LT" ||
        vr == "OB" || vr == "OD" || vr == "OF" || vr == "OW" || vr == "PN" ||
        vr == "SH" || vr == "ST" || vr == "TM" || vr == "UC" || vr == "UI" ||
        vr == "UN" || vr == "UR" || vr == "UT")
    {
        string s = Encoding.ASCII.GetString(data, offset, len).TrimEnd('\0');
        s = s.Replace("\r", "\\r").Replace("\n", "\\n");
        if (s.Length > 60) s = s[..60] + "...";
        return $"[{len}] \"{s}\"";
    }

    if (vr == "US" || vr == "SS")
    {
        if (len == 2)
        {
            return $"[{len}] {ReadU16(data, offset)}";
        }
    }

    if (vr == "UL" || vr == "SL")
    {
        if (len == 4)
        {
            return $"[{len}] {ReadU32(data, offset)}";
        }
    }

    // Default hex preview
    StringBuilder sb = new();
    for (int i = 0; i < Math.Min(len, 16); i++)
    {
        sb.AppendFormat("{0:X2} ", data[offset + i]);
    }
    if (len > 16) sb.Append("...");
    return $"[{len}] {sb}".Trim();
}
