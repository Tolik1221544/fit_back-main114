[ApiController]
[Route("api/audio-test")]
[Authorize]
public class AudioTestController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AudioTestController> _logger;
    private static readonly Dictionary<string, AudioTestFile> _files = new();

    public AudioTestController(IWebHostEnvironment environment, ILogger<AudioTestController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// ?? ��������� ����� ��� ������������ (����� 1 ���)
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAudio(IFormFile audioFile, [FromForm] string? description = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { error = "No audio file provided" });

            // ������� ����� ��� �������� �����
            var testAudioDir = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "test-audio");
            Directory.CreateDirectory(testAudioDir);

            // ���������� ���������� ��� �����
            var fileId = Guid.NewGuid().ToString("N")[..8]; // 8 �������� ��� ��������
            var extension = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                extension = ".wav";

            var fileName = $"{fileId}_{DateTime.UtcNow:MMdd_HHmm}{extension}";
            var filePath = Path.Combine(testAudioDir, fileName);

            // ��������� ����
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await audioFile.CopyToAsync(stream);
            }

            // ��������� � ������
            var testFile = new AudioTestFile
            {
                Id = fileId,
                FileName = fileName,
                FilePath = filePath,
                OriginalName = audioFile.FileName,
                Description = description ?? "Test audio",
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // ����� 1 ���
                FileSize = audioFile.Length,
                ContentType = audioFile.ContentType
            };

            _files[fileId] = testFile;

            _logger.LogInformation($"?? Uploaded test audio: {fileId} by {userId}");

            return Ok(new
            {
                fileId = fileId,
                fileName = fileName,
                originalName = audioFile.FileName,
                description = testFile.Description,
                uploadedAt = testFile.UploadedAt,
                expiresAt = testFile.ExpiresAt,
                fileSize = audioFile.Length,
                downloadUrl = $"/api/audio-test/download/{fileId}",
                testUrl = $"/api/audio-test/test/{fileId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"? Error uploading test audio: {ex.Message}");
            return BadRequest(new { error = "Failed to upload audio" });
        }
    }

    /// <summary>
    /// ?? ������ ����������� ����� ������
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListAudioFiles()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // ������� �������� �����
        CleanupExpiredFiles();

        var userFiles = _files.Values
            .Where(f => f.UploadedBy == userId)
            .OrderByDescending(f => f.UploadedAt)
            .Select(f => new
            {
                fileId = f.Id,
                fileName = f.FileName,
                originalName = f.OriginalName,
                description = f.Description,
                uploadedAt = f.UploadedAt,
                expiresAt = f.ExpiresAt,
                fileSize = f.FileSize,
                downloadUrl = $"/api/audio-test/download/{f.Id}",
                testUrl = $"/api/audio-test/test/{f.Id}",
                isExpired = f.ExpiresAt < DateTime.UtcNow
            });

        return Ok(new
        {
            files = userFiles,
            totalFiles = userFiles.Count(),
            note = "����� ������������� ��������� ����� 1 ���"
        });
    }

    /// <summary>
    /// ?? ������� ����� ����
    /// </summary>
    [HttpGet("download/{fileId}")]
    public async Task<IActionResult> DownloadAudio(string fileId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!_files.TryGetValue(fileId, out var testFile))
            return NotFound(new { error = "File not found" });

        if (testFile.UploadedBy != userId)
            return Forbid();

        if (testFile.ExpiresAt < DateTime.UtcNow)
        {
            CleanupExpiredFiles();
            return Gone(new { error = "File expired and was deleted" });
        }

        if (!System.IO.File.Exists(testFile.FilePath))
            return NotFound(new { error = "Physical file not found" });

        var fileBytes = await System.IO.File.ReadAllBytesAsync(testFile.FilePath);

        _logger.LogInformation($"?? Downloaded test audio: {fileId} by {userId}");

        return File(fileBytes, testFile.ContentType ?? "audio/wav", testFile.OriginalName);
    }

    /// <summary>
    /// ?? ����������� ����� (�������� ��� �������� �������������)
    /// </summary>
    [HttpPost("test/{fileId}")]
    public async Task<IActionResult> TestAudio(string fileId, [FromBody] TestAudioRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!_files.TryGetValue(fileId, out var testFile))
            return NotFound(new { error = "File not found" });

        if (testFile.UploadedBy != userId)
            return Forbid();

        if (testFile.ExpiresAt < DateTime.UtcNow)
            return Gone(new { error = "File expired" });

        return Ok(new
        {
            fileId = fileId,
            testType = request.TestType,
            message = "������������ ����� ���� ����������. ���� �������� � ����� ���� ������ ��� �������� ������������.",
            downloadUrl = $"/api/audio-test/download/{fileId}",
            expiresAt = testFile.ExpiresAt,
            note = "��������� ������� �������� ���������. ����������� ����-������."
        });
    }

    /// <summary>
    /// ??? ������� ����� ����
    /// </summary>
    [HttpDelete("delete/{fileId}")]
    public IActionResult DeleteAudio(string fileId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!_files.TryGetValue(fileId, out var testFile))
            return NotFound(new { error = "File not found" });

        if (testFile.UploadedBy != userId)
            return Forbid();

        // ������� ����
        if (System.IO.File.Exists(testFile.FilePath))
        {
            try
            {
                System.IO.File.Delete(testFile.FilePath);
                _logger.LogInformation($"??? Deleted test audio file: {testFile.FilePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Error deleting file: {ex.Message}");
            }
        }

        // ������� �� ������
        _files.Remove(fileId);

        return Ok(new { deleted = true, fileId = fileId });
    }

    /// <summary>
    /// ?? �������� �������� ����� (����� �������)
    /// </summary>
    [HttpPost("cleanup")]
    public IActionResult CleanupFiles()
    {
        var deletedCount = CleanupExpiredFiles();
        return Ok(new
        {
            cleaned = true,
            deletedFiles = deletedCount,
            remainingFiles = _files.Count
        });
    }

    // ��������������� ����� ��� �������
    private int CleanupExpiredFiles()
    {
        var expiredFiles = _files.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow).ToList();

        foreach (var (fileId, testFile) in expiredFiles)
        {
            // ������� ���������� ����
            if (System.IO.File.Exists(testFile.FilePath))
            {
                try
                {
                    System.IO.File.Delete(testFile.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"? Error deleting expired file: {ex.Message}");
                }
            }

            // ������� �� ������
            _files.Remove(fileId);
        }

        if (expiredFiles.Count > 0)
        {
            _logger.LogInformation($"?? Cleaned up {expiredFiles.Count} expired test audio files");
        }

        return expiredFiles.Count;
    }
}