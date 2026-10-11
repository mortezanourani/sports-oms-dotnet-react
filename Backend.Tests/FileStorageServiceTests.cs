using Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Tests;

public class FileStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenValidPng_SavesFileToConfiguredRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"backend-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var env = new TestWebHostEnvironment { ContentRootPath = root };
            var options = Options.Create(new FileStorageOptions
            {
                MaxFileSizeBytes = 1024 * 1024,
                RootPath = "custom-uploads",
                AllowedImageExtensions = new[] { ".png" },
                AllowedDocumentExtensions = new[] { ".png" }
            });

            var sut = new FileStorageService(env, options);
            using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 });
            var file = new FormFile(stream, 0, stream.Length, "file", "image.png");

            var result = await sut.SaveAsync(file, "images", isPublic: true, isImage: true);

            Assert.True(result.Success);
            Assert.NotNull(result.RelativePath);
            Assert.StartsWith("public/images/", result.RelativePath);
            Assert.EndsWith(".png", result.RelativePath);
            Assert.True(File.Exists(Path.Combine(root, "custom-uploads", result.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenPrivateFile_SavesUnderPrivateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"backend-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sut = new FileStorageService(
                new TestWebHostEnvironment { ContentRootPath = root },
                Options.Create(new FileStorageOptions
                {
                    RootPath = "uploads",
                    AllowedDocumentExtensions = new[] { ".png" }
                }));
            using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var file = new FormFile(stream, 0, stream.Length, "file", "private.png");

            var result = await sut.SaveAsync(file, "messages", isPublic: false, isImage: false);

            Assert.True(result.Success);
            Assert.StartsWith("private/messages/", result.RelativePath);
            Assert.True(File.Exists(sut.GetFullPath(result.RelativePath!)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenFileExceedsMaxSize_ReturnsFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), $"backend-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var env = new TestWebHostEnvironment { ContentRootPath = root };
            var options = Options.Create(new FileStorageOptions
            {
                MaxFileSizeBytes = 16,
                RootPath = "custom-uploads",
                AllowedImageExtensions = new[] { ".png" },
                AllowedDocumentExtensions = new[] { ".png" }
            });

            var sut = new FileStorageService(env, options);
            using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            var file = new FormFile(stream, 0, stream.Length, "file", "image.png");

            var result = await sut.SaveAsync(file, "images", isPublic: true, isImage: true);

            Assert.False(result.Success);
            Assert.Contains("نباید بیشتر از", result.Error!);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("public/../private/file.png")]
    [InlineData("public\\..\\private\\file.png")]
    public void GetFullPath_WhenPathEscapesOrNormalizesSegments_Throws(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"backend-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sut = new FileStorageService(
                new TestWebHostEnvironment { ContentRootPath = root },
                Options.Create(new FileStorageOptions { RootPath = "uploads" }));

            Assert.Throws<ArgumentException>(() => sut.GetFullPath(relativePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveAbsolutePath_ProductionRequiresAbsoluteConfiguredRoot()
    {
        var options = new FileStorageOptions { RootPath = "Uploads" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            options.ResolveAbsolutePath(Path.GetTempPath(), requireConfiguredAbsolutePath: true));

        Assert.Contains("absolute path", exception.Message);
    }

    [Fact]
    public void ResolveAbsolutePath_DevelopmentAllowsDefaultRoot()
    {
        var contentRoot = Path.GetTempPath();
        var options = new FileStorageOptions();

        Assert.Equal(Path.Combine(contentRoot, "Uploads"), options.ResolveAbsolutePath(
            contentRoot, requireConfiguredAbsolutePath: false));
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Backend.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Production;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}
