using FellowOakDicom;

namespace cli.Tests;

public sealed class CompareFlowTests
{
    [Fact]
    public async Task CompareWithChangedAddedAndRemovedAttributesWritesDifferences()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-compare-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            var leftDataset = CreateBaseDataset();
            leftDataset.Add(DicomTag.PatientName, "Doe^Jane");
            leftDataset.Add(DicomTag.PatientID, "12345");
            var rightDataset = CreateBaseDataset();
            rightDataset.Add(DicomTag.Modality, "MR");
            rightDataset.Add(DicomTag.PatientName, "Doe^John");
            await WriteDicomAsync(leftFile, leftDataset);
            await WriteDicomAsync(rightFile, rightDataset);

            var result = ExecuteCompare(leftFile, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("+ 00080060 CS Modality", result.Output);
            Assert.Contains("  right: MR", result.Output);
            Assert.Contains("~ 00100010 PN Patient's Name", result.Output);
            Assert.Contains("  left:  Doe^Jane", result.Output);
            Assert.Contains("  right: Doe^John", result.Output);
            Assert.Contains("- 00100020 LO Patient ID", result.Output);
            Assert.Contains("  left:  12345", result.Output);
            Assert.DoesNotContain("1.2.826.0.1.3680043.10.999.1", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CompareWithNestedSequenceDifferenceWritesItemPath()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-compare-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            var leftDataset = CreateBaseDataset();
            leftDataset.Add(CreateReferencedStudySequence("1.2.826.0.1.3680043.10.999.101"));
            var rightDataset = CreateBaseDataset();
            rightDataset.Add(CreateReferencedStudySequence("1.2.826.0.1.3680043.10.999.102"));
            await WriteDicomAsync(leftFile, leftDataset);
            await WriteDicomAsync(rightFile, rightDataset);

            var result = ExecuteCompare(leftFile, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("~ 00081110[1].00081155 UI Referenced SOP Instance UID", result.Output);
            Assert.Contains("  left:  1.2.826.0.1.3680043.10.999.101", result.Output);
            Assert.Contains("  right: 1.2.826.0.1.3680043.10.999.102", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CompareWithBinaryDifferenceWritesSafeSummary()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-compare-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            var leftDataset = CreateBaseDataset();
            leftDataset.Add(new DicomOtherByte(DicomTag.PixelData, [1, 2, 3]));
            var rightDataset = CreateBaseDataset();
            rightDataset.Add(new DicomOtherByte(DicomTag.PixelData, [1, 2, 4]));
            await WriteDicomAsync(leftFile, leftDataset);
            await WriteDicomAsync(rightFile, rightDataset);

            var result = ExecuteCompare(leftFile, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("~ 7FE00010 OB Pixel Data", result.Output);
            Assert.Contains("[3 bytes, sha256:", result.Output);
            Assert.DoesNotContain("AQID", result.Output);
            Assert.DoesNotContain("010203", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CompareWithEmptyValueAndLiteralEmptySentinelWritesDifference()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-compare-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            var leftDataset = CreateBaseDataset();
            leftDataset.Add(DicomTag.PatientID, string.Empty);
            var rightDataset = CreateBaseDataset();
            rightDataset.Add(DicomTag.PatientID, "[empty]");
            await WriteDicomAsync(leftFile, leftDataset);
            await WriteDicomAsync(rightFile, rightDataset);

            var result = ExecuteCompare(leftFile, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("~ 00100020 LO Patient ID", result.Output);
            Assert.Contains("  left:  \"\"", result.Output);
            Assert.Contains("  right: [empty]", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void CompareWithInvalidInputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = ExecuteCompare("left.txt", "right.dcm");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void CompareWithMissingFileReturnsFailureAndErrorMessage()
    {
        var result = ExecuteCompare("left.dcm", "right.dcm");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("File not found: left.dcm", result.Error);
        Assert.Empty(result.Output);
    }

    private static DicomDataset CreateBaseDataset()
    {
        return new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.CTImageStorage },
            { DicomTag.SOPInstanceUID, "1.2.826.0.1.3680043.10.999.1" }
        };
    }

    private static DicomSequence CreateReferencedStudySequence(string referencedSopInstanceUid)
    {
        return new DicomSequence(
            DicomTag.ReferencedStudySequence,
            new DicomDataset
            {
                { DicomTag.ReferencedSOPClassUID, DicomUID.CTImageStorage },
                { DicomTag.ReferencedSOPInstanceUID, referencedSopInstanceUid }
            });
    }

    private static Task WriteDicomAsync(string path, DicomDataset dataset)
    {
        TestDicomFiles.EnsureDicomSetup();
        return new DicomFile(dataset).SaveAsync(path);
    }

    private static FlowResult ExecuteCompare(string leftPath, string rightPath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new CompareCommand(leftPath, rightPath), output, error);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
