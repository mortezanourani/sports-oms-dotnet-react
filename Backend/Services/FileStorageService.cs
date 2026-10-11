using System.IO.Compression;

namespace Backend.Services
{
    public class FileStorageOptions
    {
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
        public string RootPath { get; set; } = string.Empty;
        public string[] AllowedImageExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".webp" };
        public string[] AllowedDocumentExtensions { get; set; } = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xlsx" };

        public string ResolveAbsolutePath(string contentRootPath)
        {
            if (string.IsNullOrWhiteSpace(RootPath))
                return Path.Combine(contentRootPath, "Uploads");

            return Path.IsPathRooted(RootPath)
                ? RootPath
                : Path.GetFullPath(Path.Combine(contentRootPath, RootPath));
        }
    }
    public class FileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly FileStorageOptions _options;

        public FileStorageService(IWebHostEnvironment env, Microsoft.Extensions.Options.IOptions<FileStorageOptions> options)
        {
            _env = env;
            _options = options.Value;
        }

        // isPublic determines which root folder the file lands in, which in
        // turn determines whether it ends up reachable by a direct URL
        // (public) or only through an authorized controller action (private).
        // subfolder groups files by entity type on disk (e.g. "champions",
        // "facility-contracts") purely for organization/debuggability —
        // it has no effect on access control.
        public async Task<(bool Success, string? RelativePath, string? Error)> SaveAsync(
            IFormFile file, string subfolder, bool isPublic, bool isImage)
        {
            if (file == null || file.Length == 0)
                return (false, null, "فایلی ارسال نشده است");

            if (file.Length > _options.MaxFileSizeBytes)
                return (false, null, $"حجم فایل نباید بیشتر از {_options.MaxFileSizeBytes / 1024 / 1024} مگابایت باشد");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = isImage ? _options.AllowedImageExtensions : _options.AllowedDocumentExtensions;

            if (!allowedExtensions.Contains(extension))
                return (false, null, $"نوع فایل مجاز نیست. فرمت‌های مجاز: {string.Join(", ", allowedExtensions)}");

            if (!await HasValidContentAsync(file, extension))
                return (false, null, "محتوای فایل با پسوند آن مطابقت ندارد");

            // The uploaded file's ORIGINAL name is never used as the stored
            // filename. Two reasons: (1) it can contain characters or path
            // segments (e.g. "../../something") that could be used for a
            // path traversal attack if concatenated into a file path without
            // sanitization; (2) two different uploads with the same
            // filename would otherwise silently overwrite each other. A
            // fresh Guid sidesteps both problems entirely, at the cost of
            // needing to remember the original filename separately if you
            // ever want to show it to the user (MessageAttachment does this
            // by storing the original name in Path.GetFileName at display
            // time currently — worth revisiting if users need to see their
            // real uploaded filenames rather than a generated one).
            var storedFileName = $"{Guid.NewGuid()}{extension}";

            var rootFolder = isPublic ? "public" : "private";
            var relativePath = Path.Combine(rootFolder, subfolder, storedFileName);
            var fullPath = ResolveUploadPath(relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            // The RelativePath stored in the database deliberately includes
            // the public/private root, so anything reading this path later
            // (the download controller, a migration script, etc.) knows
            // unambiguously which physical folder to look in without having
            // to separately track "was this public or private" elsewhere.
            return (true, relativePath.Replace("\\", "/"), null);
        }

        public void Delete(string relativePath)
        {
            var fullPath = ResolveUploadPath(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public string GetFullPath(string relativePath) =>
            ResolveUploadPath(relativePath);

        private static async Task<bool> HasValidContentAsync(IFormFile file, string extension)
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var totalRead = 0;
            while (totalRead < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(totalRead));
                if (read == 0) break;
                totalRead += read;
            }

            return extension switch
            {
                ".jpg" or ".jpeg" => totalRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => totalRead >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".webp" => totalRead >= 12 &&
                           header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                           header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                ".pdf" => totalRead >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                ".doc" => totalRead >= 8 &&
                          header.AsSpan(0, 8).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
                ".docx" => await IsOpenXmlDocumentAsync(stream, "word/document.xml"),
                ".xlsx" => await IsOpenXmlDocumentAsync(stream, "xl/workbook.xml"),
                _ => false
            };
        }

        private static async Task<bool> IsOpenXmlDocumentAsync(Stream stream, string expectedEntry)
        {
            if (!stream.CanSeek) return false;

            stream.Position = 0;
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                return archive.GetEntry("[Content_Types].xml") != null &&
                       archive.GetEntry(expectedEntry) != null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private string ResolveUploadPath(string relativePath)
        {
            var uploadsRoot = Path.GetFullPath(_options.ResolveAbsolutePath(_env.ContentRootPath));
            var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, relativePath));
            var rootPrefix = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
                ? uploadsRoot
                : uploadsRoot + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(rootPrefix, comparison))
                throw new ArgumentException("The file path must be inside the uploads directory.", nameof(relativePath));

            return fullPath;
        }
    }
}