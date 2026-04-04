using System.Security.Cryptography;
using ApartmentManagement.API.V1.DTOs.Uploads;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Performance;
using ApartmentManagement.Settings;
using ApartmentManagement.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApartmentManagement.Infrastructure;

/// <summary>
/// Stores files only under <c>{BasePath}/avatars/{userId}/</c> and <c>{BasePath}/apartmentImages/{apartmentId}/</c>.
/// This API does not map uploads as anonymous static files; serve images through authenticated endpoints if needed.
/// </summary>
public sealed class UploadValidatorService : IUploadValidator
{
    private const int MagicHeaderReadBytes = 32;

    private readonly UploadSettings _settings;
    private readonly ILogger<UploadValidatorService> _logger;
    private readonly IMalwareScanner _malwareScanner;
    private readonly UploadValidationTelemetry _telemetry;

    public UploadValidatorService(
        IOptions<UploadSettings> settings,
        ILogger<UploadValidatorService> logger,
        IMalwareScanner malwareScanner,
        UploadValidationTelemetry telemetry)
    {
        _settings = settings.Value;
        _logger = logger;
        _malwareScanner = malwareScanner;
        _telemetry = telemetry;

        EnsureUploadDirectories();
    }

    private void EnsureUploadDirectories()
    {
        var root = UploadPathSecurity.NormalizeDirectory(_settings.BasePath);
        Directory.CreateDirectory(root);

        var stagingRoot = UploadPathSecurity.NormalizeDirectory(_settings.StagingPath);
        Directory.CreateDirectory(stagingRoot);
        UploadPathSecurity.AssertPathUnderRoot(root, stagingRoot);

        Directory.CreateDirectory(Path.Combine(root, "avatars"));
        Directory.CreateDirectory(Path.Combine(root, "apartmentImages"));
    }

    public Task ValidateRequestAsync(IReadOnlyCollection<IFormFile> files, CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
            throw new InvalidOperationException("No upload file was provided.");

        if (files.Count > _settings.MaxFilesPerRequest)
            throw new InvalidOperationException($"Too many files in one request. Max allowed is {_settings.MaxFilesPerRequest}.");

        return Task.CompletedTask;
    }

    public Task ValidateImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        => ValidateImageMetadataAsync(file, cancellationToken);

    public Task<UploadResultDto> SaveUserAvatarAsync(IFormFile file, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A valid user id is required to store an avatar.");
        var category = $"avatars/{userId:N}";
        return SaveImageCoreAsync(file, category, clearTargetDirectoryFirst: true, skipDuplicateDetection: true, cancellationToken);
    }

    public Task<UploadResultDto> SaveApartmentImageAsync(IFormFile file, Guid apartmentId, CancellationToken cancellationToken = default)
    {
        if (apartmentId == Guid.Empty)
            throw new InvalidOperationException("A valid apartment id is required to store an image.");
        var category = $"apartmentImages/{apartmentId:N}";
        return SaveImageCoreAsync(file, category, clearTargetDirectoryFirst: false, skipDuplicateDetection: false, cancellationToken);
    }

    private async Task<UploadResultDto> SaveImageCoreAsync(
        IFormFile file,
        string category,
        bool clearTargetDirectoryFirst,
        bool skipDuplicateDetection,
        CancellationToken cancellationToken)
    {
        _telemetry.RecordAttempt();
        try
        {
            await ValidateImageMetadataAsync(file, cancellationToken);

            var safeCategory = SanitizeCategory(category);
            AssertAllowedUploadSubtree(safeCategory);

            var root = UploadPathSecurity.NormalizeDirectory(_settings.BasePath);
            Directory.CreateDirectory(root);

            var stagingRoot = UploadPathSecurity.NormalizeDirectory(_settings.StagingPath);
            Directory.CreateDirectory(stagingRoot);
            UploadPathSecurity.AssertPathUnderRoot(root, stagingRoot);

            var targetDir = Path.Combine(root, safeCategory);
            var targetDirFull = Path.GetFullPath(targetDir);
            UploadPathSecurity.AssertPathUnderRoot(root, targetDirFull);

            if (clearTargetDirectoryFirst && Directory.Exists(targetDirFull))
            {
                foreach (var existing in Directory.GetFiles(targetDirFull))
                {
                    try
                    {
                        File.Delete(existing);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete previous file: {Path}", existing);
                    }
                }
            }

            Directory.CreateDirectory(targetDirFull);
            var contentType = NormalizeDeclaredMime(file.ContentType);
            var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();

            var tempName = $"{Guid.NewGuid():N}.tmp";
            var tempPath = Path.GetFullPath(Path.Combine(stagingRoot, tempName));
            UploadPathSecurity.AssertPathUnderRoot(stagingRoot, tempPath);

            await using (var temp = File.Create(tempPath))
            {
                await file.CopyToAsync(temp, cancellationToken);
            }

            try
            {
                if (!File.Exists(tempPath))
                    throw new InvalidOperationException("Temporary upload file was not created.");

                var writtenLength = new FileInfo(tempPath).Length;
                if (writtenLength != file.Length)
                    throw new InvalidOperationException("Uploaded file size does not match the received stream.");

                await AssertTempFileIsValidImageAsync(tempPath, extension, contentType, cancellationToken);

                var hash = await ComputeSha256Async(tempPath, cancellationToken);
                var fileName = $"{hash}{extension}";
                var targetPath = Path.GetFullPath(Path.Combine(targetDirFull, fileName));
                UploadPathSecurity.AssertPathUnderRoot(root, targetPath);

                if (!skipDuplicateDetection && _settings.EnableDuplicateDetection && File.Exists(targetPath))
                {
                    _logger.LogWarning("Duplicate upload detected. Path={Path} Hash={Hash}", targetPath, hash);
                    throw new InvalidOperationException("Duplicate image detected.");
                }

                var dimensions = ImageMetadataHelper.ReadDimensionsAutoDetect(tempPath);
                if (dimensions.Width > _settings.MaxImageWidth || dimensions.Height > _settings.MaxImageHeight)
                {
                    throw new InvalidOperationException(
                        $"Image dimensions exceed allowed maximum ({_settings.MaxImageWidth}x{_settings.MaxImageHeight}).");
                }

                if (_settings.EnableMalwareScan)
                {
                    var scan = await _malwareScanner.ScanAsync(tempPath, cancellationToken);
                    if (!scan.IsClean)
                        throw new InvalidOperationException(scan.Reason ?? "Upload failed malware scan.");
                }

                File.Move(tempPath, targetPath, overwrite: true);

                _telemetry.RecordSuccess();
                _logger.LogInformation(
                    "Saved uploaded file. Category={Category} Path={Path} Hash={Hash} Width={Width} Height={Height}",
                    safeCategory, targetPath, hash, dimensions.Width, dimensions.Height);

                return new UploadResultDto
                {
                    FilePath = targetPath,
                    FileName = fileName,
                    MimeType = contentType,
                    Size = file.Length
                };
            }
            finally
            {
                TryDelete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _telemetry.RecordFailure();
            var snapshot = _telemetry.Snapshot();
            _logger.LogWarning(ex, "Upload validation failed. Attempts={Attempts} Failures={Failures} FailureRate={FailureRate:P2}", snapshot.Attempts, snapshot.Failures, snapshot.FailureRate);
            throw;
        }
    }

    private static void AssertAllowedUploadSubtree(string safeCategory)
    {
        var parts = safeCategory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new InvalidOperationException("Invalid upload path structure.");
        var top = parts[0];
        if (!string.Equals(top, "avatars", StringComparison.Ordinal) &&
            !string.Equals(top, "apartmentImages", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Uploads are only allowed under 'avatars' or 'apartmentImages'.");
        }
    }

    private async Task ValidateImageMetadataAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
            throw new InvalidOperationException("File is missing.");

        if (file.FileName.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file name.");

        var originalName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalName))
            throw new InvalidOperationException("File name is missing.");

        if (file.Length <= 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > _settings.MaxFileSizeBytes)
            throw new InvalidOperationException($"File is too large. Max allowed is {_settings.MaxFileSizeBytes} bytes.");

        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (!_settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file extension.");

        var contentType = NormalizeDeclaredMime(file.ContentType);
        if (string.IsNullOrEmpty(contentType) || !_settings.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid MIME type.");

        await using var stream = file.OpenReadStream();
        var header = new byte[MagicHeaderReadBytes];
        var read = await stream.ReadAsync(header.AsMemory(0, MagicHeaderReadBytes), cancellationToken);
        if (read < 12)
            throw new InvalidOperationException("Could not read file header (file too small or empty stream).");

        var detected = FileSignatureHelper.DetectFormat(header.AsSpan(0, read));
        if (detected == DetectedImageFormat.None)
            throw new InvalidOperationException("File signature is invalid or file is not a supported image.");

        if (!FileSignatureHelper.ExtensionMatchesFormat(extension, detected))
            throw new InvalidOperationException("File extension does not match the actual image format (possible fake image).");

        var canonical = FileSignatureHelper.ToCanonicalMimeType(detected);
        if (!string.Equals(contentType, canonical, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Content-Type does not match the file content (possible fake image).");

        _logger.LogInformation("Validated upload metadata: {FileName} {Size} bytes {MimeType}", originalName, file.Length, contentType);
    }

    private async Task AssertTempFileIsValidImageAsync(
        string tempPath,
        string extensionLower,
        string normalizedMime,
        CancellationToken cancellationToken)
    {
        await using var fs = File.OpenRead(tempPath);
        var header = new byte[MagicHeaderReadBytes];
        var read = await fs.ReadAsync(header.AsMemory(0, MagicHeaderReadBytes), cancellationToken);
        if (read < 12)
            throw new InvalidOperationException("Stored file is too small to be a valid image.");

        var detected = FileSignatureHelper.DetectFormat(header.AsSpan(0, read));
        if (detected == DetectedImageFormat.None)
            throw new InvalidOperationException("Stored file failed content inspection.");

        if (!FileSignatureHelper.ExtensionMatchesFormat(extensionLower, detected))
            throw new InvalidOperationException("Stored file extension does not match content.");

        var canonical = FileSignatureHelper.ToCanonicalMimeType(detected);
        if (!string.Equals(normalizedMime, canonical, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored file MIME does not match content.");
    }

    private static string NormalizeDeclaredMime(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;
        var t = contentType.Trim().ToLowerInvariant();
        return t switch
        {
            "image/jpg" or "image/pjpeg" => "image/jpeg",
            _ => t
        };
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeCategory(string category)
    {
        var rawSegments = string.IsNullOrWhiteSpace(category)
            ? new[] { "misc" }
            : category.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        var segments = rawSegments
            .Select(SanitizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return segments.Length == 0 ? "misc" : Path.Combine(segments);
    }

    private static string SanitizeSegment(string segment)
    {
        var cleaned = new string(segment.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim();
        cleaned = cleaned.Replace("..", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
