using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Performance;
using ApartmentManagement.Settings;
using ApartmentManagement.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ApartmentManagement.UnitTests.Services;

public sealed class UploadValidatorServiceTests : IDisposable
{
    // 1x1 transparent PNG
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _root;

    public UploadValidatorServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "upload_ut_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            /* ignore */
        }
    }

    private UploadValidatorService CreateSut(UploadSettings? overrides = null)
    {
        var settings = overrides ?? new UploadSettings();
        settings.BasePath = _root;
        settings.StagingPath = Path.Combine(_root, "_staging");
        settings.EnableMalwareScan = false;
        settings.EnableDuplicateDetection = false;
        settings.MaxFileSizeBytes = Math.Max(settings.MaxFileSizeBytes, TinyPng.Length);

        var scanner = new Mock<IMalwareScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MalwareScanResult.Clean);

        return new UploadValidatorService(
            Options.Create(settings),
            NullLogger<UploadValidatorService>.Instance,
            scanner.Object,
            new UploadValidationTelemetry());
    }

    private static Mock<IFormFile> CreatePngFileMock()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(TinyPng.Length);
        file.Setup(f => f.FileName).Returns("tiny.png");
        file.Setup(f => f.ContentType).Returns("image/png");
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(TinyPng));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((dest, ct) =>
                dest.WriteAsync(TinyPng.AsMemory(0, TinyPng.Length), ct).AsTask());
        return file;
    }

    [Fact]
    public async Task ValidateRequestAsync_Empty_Throws()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ValidateRequestAsync(Array.Empty<IFormFile>()));
    }

    [Fact]
    public async Task ValidateImageAsync_Png_With_Jpg_Extension_Throws()
    {
        var sut = CreateSut();
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(TinyPng.Length);
        file.Setup(f => f.FileName).Returns("fake.jpg");
        file.Setup(f => f.ContentType).Returns("image/jpeg");
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(TinyPng));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ValidateImageAsync(file.Object, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateImageAsync_ValidPng_Does_Not_Throw()
    {
        var sut = CreateSut();
        var file = CreatePngFileMock();
        await sut.Invoking(s => s.ValidateImageAsync(file.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveApartmentImageAsync_Writes_Under_ApartmentImages_And_GuidN_Folder()
    {
        var sut = CreateSut();
        var apartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var file = CreatePngFileMock();

        var result = await sut.SaveApartmentImageAsync(file.Object, apartmentId, CancellationToken.None);

        var expectedDir = Path.Combine(_root, "apartmentImages", apartmentId.ToString("N"));
        result.FilePath.Should().StartWith(expectedDir);
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveUserAvatarAsync_Writes_Under_Avatars_And_GuidN_Folder()
    {
        var sut = CreateSut();
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var file = CreatePngFileMock();

        var result = await sut.SaveUserAvatarAsync(file.Object, userId, CancellationToken.None);

        var expectedDir = Path.Combine(_root, "avatars", userId.ToString("N"));
        result.FilePath.Should().StartWith(expectedDir);
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveUserAvatarAsync_Replaces_Previous_File_In_User_Folder()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        var file1 = CreatePngFileMock();
        var file2 = CreatePngFileMock();
        await sut.SaveUserAvatarAsync(file1.Object, userId, CancellationToken.None);
        await sut.SaveUserAvatarAsync(file2.Object, userId, CancellationToken.None);

        var dir = Path.Combine(_root, "avatars", userId.ToString("N"));
        Directory.GetFiles(dir).Should().HaveCount(1);
    }
}
