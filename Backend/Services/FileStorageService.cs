namespace Backend.Services
{
    public class FileStorageOptions
    {
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
        public string[] AllowedImageExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".webp" };
        public string[] AllowedDocumentExtensions { get; set; } = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
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
            var uploadsRoot = Path.Combine(_env.ContentRootPath, "Uploads", rootFolder, subfolder);

            Directory.CreateDirectory(uploadsRoot); // no-op if it already exists

            var fullPath = Path.Combine(uploadsRoot, storedFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // The RelativePath stored in the database deliberately includes
            // the public/private root, so anything reading this path later
            // (the download controller, a migration script, etc.) knows
            // unambiguously which physical folder to look in without having
            // to separately track "was this public or private" elsewhere.
            var relativePath = Path.Combine(rootFolder, subfolder, storedFileName).Replace("\\", "/");
            return (true, relativePath, null);
        }

        public void Delete(string relativePath)
        {
            var fullPath = Path.Combine(_env.ContentRootPath, "Uploads", relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public string GetFullPath(string relativePath) =>
            Path.Combine(_env.ContentRootPath, "Uploads", relativePath);
    }
}