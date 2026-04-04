using ApartmentManagement.Utilities;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Utilities;

public sealed class FileSignatureHelperTests
{
    private static readonly byte[] PngHeader =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    };

    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    private static readonly byte[] WebpHeader =
    {
        (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
        (byte)'W', (byte)'E', (byte)'B', (byte)'P'
    };

    [Fact]
    public void DetectFormat_Png()
    {
        FileSignatureHelper.DetectFormat(PngHeader).Should().Be(DetectedImageFormat.Png);
    }

    [Fact]
    public void DetectFormat_Jpeg()
    {
        FileSignatureHelper.DetectFormat(JpegHeader).Should().Be(DetectedImageFormat.Jpeg);
    }

    [Fact]
    public void DetectFormat_Webp()
    {
        FileSignatureHelper.DetectFormat(WebpHeader).Should().Be(DetectedImageFormat.Webp);
    }

    [Fact]
    public void DetectFormat_Unknown_Returns_None()
    {
        FileSignatureHelper.DetectFormat(new byte[] { 0x00, 0x01, 0x02 }).Should().Be(DetectedImageFormat.None);
    }

    [Theory]
    [InlineData(DetectedImageFormat.Jpeg, ".jpg", true)]
    [InlineData(DetectedImageFormat.Jpeg, ".jpeg", true)]
    [InlineData(DetectedImageFormat.Jpeg, ".png", false)]
    [InlineData(DetectedImageFormat.Png, ".png", true)]
    [InlineData(DetectedImageFormat.Webp, ".webp", true)]
    public void ExtensionMatchesFormat(DetectedImageFormat format, string ext, bool expected)
    {
        FileSignatureHelper.ExtensionMatchesFormat(ext, format).Should().Be(expected);
    }
}
