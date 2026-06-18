using System.Text;
using System.Xml;
using FellowOakDicom;

internal static class DicomExtractWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryGetBinaryData(DicomItem item, out byte[] data)
    {
        if (item is DicomFragmentSequence fragmentSequence)
        {
            data = fragmentSequence.Fragments.SelectMany(buffer => buffer?.Data ?? Array.Empty<byte>()).ToArray();
            return true;
        }

        if (item is DicomElement element && IsBinaryVR(element.ValueRepresentation))
        {
            data = element.Buffer?.Data ?? Array.Empty<byte>();
            return true;
        }

        data = Array.Empty<byte>();
        return false;
    }

    public static void Write(byte[] data, ExtractFormat format, TextWriter output)
    {
        switch (format)
        {
            case ExtractFormat.Base64:
                output.WriteLine(Convert.ToBase64String(data));
                break;
            case ExtractFormat.Hex:
                output.WriteLine(Convert.ToHexString(data));
                break;
            case ExtractFormat.Xml:
                WriteXml(data, output);
                break;
            default:
                throw new InvalidOperationException($"Unknown extract format: {format}");
        }
    }

    private static void WriteXml(byte[] data, TextWriter output)
    {
        var document = ReadXmlDocument(StrictUtf8.GetString(TrimTrailingNulls(data)));
        MaskSetupPhotoPictures(document);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = document.FirstChild is not XmlDeclaration
        };
        using var writer = XmlWriter.Create(output, settings);
        document.WriteTo(writer);
        writer.Flush();
        output.WriteLine();
    }

    private static XmlDocument ReadXmlDocument(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        var document = new XmlDocument { XmlResolver = null };
        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        document.Load(xmlReader);
        return document;
    }

    private static void MaskSetupPhotoPictures(XmlDocument document)
    {
        foreach (XmlNode node in document.GetElementsByTagName("*"))
        {
            if (node.LocalName != "SetupPhotoPicture")
            {
                continue;
            }

            var text = node.InnerText.Trim();
            node.InnerText = $"[{GetPayloadByteCount(text)} bytes]";
        }
    }

    private static int GetPayloadByteCount(string text)
    {
        return text.Length % 2 == 0 && text.All(IsHexDigit)
            ? text.Length / 2
            : StrictUtf8.GetByteCount(text);
    }

    private static bool IsHexDigit(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';
    }

    private static byte[] TrimTrailingNulls(byte[] data)
    {
        var length = data.Length;
        while (length > 0 && data[length - 1] == 0)
        {
            length--;
        }

        return length == data.Length ? data : data[..length];
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
