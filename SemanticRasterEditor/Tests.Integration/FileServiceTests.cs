using Editor.Services;
using SkiaSharp;

namespace Tests.Integration
{
    public class FileServiceTests
    {
        [Fact]
        public void SaveAndLoadImage_PreservesDimensions()
        {
            var service = new FileService();
            var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.png");

            try
            {
                using (var original = new SKBitmap(64, 48))
                {
                    service.SaveImage(original, tempPath);
                }

                using var loaded = service.OpenImage(tempPath);

                Assert.NotNull(loaded);
                Assert.Equal(64, loaded!.Width);
                Assert.Equal(48, loaded.Height);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void OpenImage_ReturnsNullForMissingFile()
        {
            var service = new FileService();

            var result = service.OpenImage("/nonexistent/path.png");

            Assert.Null(result);
        }

        [Fact]
        public void SupportedExtensions_ContainsCommonFormats()
        {
            Assert.Contains(".png", FileService.SupportedExtensions);
            Assert.Contains(".jpg", FileService.SupportedExtensions);
            Assert.Contains(".jpeg", FileService.SupportedExtensions);
            Assert.Contains(".bmp", FileService.SupportedExtensions);
            Assert.Contains(".webp", FileService.SupportedExtensions);
        }
    }
}
