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

// Chỉ lưu file dưới <c>{BasePath}/avatars/{userId}/</c> và <c>{BasePath}/apartmentImages/{apartmentId}/</c>. API không phục vụ static file ẩn danh; cần endpoint có xác thực nếu muốn trả ảnh.
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

        // Tạo thư mục gốc upload + staging khi khởi tạo service.
        EnsureUploadDirectories();
    }

    // đảm bảo BasePath, StagingPath tồn tại và staging nằm trong root; tạo thư mục con avatars/apartmentImages.
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

    // kiểm tra có file và không vượt MaxFilesPerRequest.
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

    // Avatar: xóa thư mục user trước (một avatar), bỏ kiểm tra trùng hash.
    public Task<UploadResultDto> SaveUserAvatarAsync(IFormFile file, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A valid user id is required to store an avatar.");
        var category = $"avatars/{userId:N}";
        return SaveImageCoreAsync(file, category, clearTargetDirectoryFirst: true, skipDuplicateDetection: true, cancellationToken);
    }

    // Ảnh căn hộ: giữ nhiều file; bật phát hiện trùng nội dung nếu cấu hình.
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
            // validate metadata stream → chuẩn hóa đường dẫn → tạo thư mục đích an toàn.
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

            // Tùy chọn: xóa file cũ trong thư mục (avatar — một file đại diện).
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

            // Ghi file tạm vào staging rồi kiểm tra kích thước, chữ ký ảnh, kích thước pixel, malware.
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

                // Đặt tên file = hash + extension; chuyển từ staging sang đích.
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
            // Ghi thất bại telemetry + snapshot tỷ lệ lỗi rồi ném lại exception gốc.
            _telemetry.RecordFailure();
            var snapshot = _telemetry.Snapshot();
            _logger.LogWarning(ex, "Upload validation failed. Attempts={Attempts} Failures={Failures} FailureRate={FailureRate:P2}", snapshot.Attempts, snapshot.Failures, snapshot.FailureRate);
            throw;
        }
    }

    // Chỉ cho phép nhánh đầu là avatars hoặc apartmentImages (chống ghi ra ngoài ý định).
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

    // kiểm tra tên file, kích thước, extension/MIME cho phép, đọc header khớp magic bytes.
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

    // kiểm tra lại file đã ghi đĩa (tránh race / tampering sau khi copy).
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

    // Chuẩn hóa alias MIME (image/jpg → image/jpeg).
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

    // Hash nội dung file (hex lowercase) để đặt tên và phát hiện trùng.
    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Tách segment đường dẫn, loại ký tự không hợp lệ và "..".
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
            // Dọn file tạm best-effort — không che giấu lỗi nghiệp vụ.
        }
    }
}
