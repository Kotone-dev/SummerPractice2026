using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    public class LayerServiceTests
    {
        [Fact]
        public void AddLayer_IncreasesCount()
        {
            var service = new LayerService();
            using var bitmap = new SKBitmap(10, 10);

            service.Add(bitmap);

            Assert.Equal(1, service.Count);
        }

        [Fact]
        public void RemoveLayer_DecreasesCount()
        {
            var service = new LayerService();
            using var bitmap = new SKBitmap(10, 10);
            service.Add(bitmap);

            service.Remove(0);

            Assert.Equal(0, service.Count);
        }

        [Fact]
        public void MoveUp_SwapsLayers()
        {
            var service = new LayerService();
            using var b1 = new SKBitmap(10, 10);
            using var b2 = new SKBitmap(10, 10);
            service.Add(b1, "Первый");
            service.Add(b2, "Второй");

            service.MoveUp(1);

            Assert.Equal("Второй", service.Layers[0].Name);
            Assert.Equal("Первый", service.Layers[1].Name);
        }

        [Fact]
        public void MoveDown_SwapsLayers()
        {
            var service = new LayerService();
            using var b1 = new SKBitmap(10, 10);
            using var b2 = new SKBitmap(10, 10);
            service.Add(b1, "Первый");
            service.Add(b2, "Второй");

            service.MoveDown(0);

            Assert.Equal("Второй", service.Layers[0].Name);
            Assert.Equal("Первый", service.Layers[1].Name);
        }

        [Fact]
        public void ActiveLayer_ReturnsCorrectLayer()
        {
            var service = new LayerService();
            using var b1 = new SKBitmap(10, 10);
            using var b2 = new SKBitmap(10, 10);
            service.Add(b1, "А");
            service.Add(b2, "Б");
            service.ActiveIndex = 0;

            Assert.Equal("А", service.ActiveLayer?.Name);
        }

        [Fact]
        public void Composite_SingleLayer_ReturnsCopy()
        {
            var service = new LayerService();
            using var bitmap = new SKBitmap(20, 15);
            service.Add(bitmap);

            using var result = service.Composite();

            Assert.Equal(20, result.Width);
            Assert.Equal(15, result.Height);
        }

        [Fact]
        public void Composite_InvisibleLayer_NotIncluded()
        {
            var service = new LayerService();
            using var red = CreateSolidBitmap(10, 10, new SKColor(255, 0, 0));
            using var green = CreateSolidBitmap(10, 10, new SKColor(0, 255, 0));
            service.Add(red);
            service.Add(green);
            service.Layers[0].IsVisible = false;

            using var result = service.Composite();
            var pixel = result.GetPixel(5, 5);

            Assert.Equal(0, pixel.Red);
            Assert.True(pixel.Green > 0);
        }

        [Fact]
        public void OpacityAffectsComposite()
        {
            var service = new LayerService();
            using var bottom = CreateSolidBitmap(10, 10, new SKColor(0, 0, 255));
            using var top = CreateSolidBitmap(10, 10, new SKColor(255, 0, 0));
            service.Add(bottom);
            service.Add(top);
            service.Layers[1].Opacity = 0.5f;

            using var result = service.Composite();
            var pixel = result.GetPixel(5, 5);

            Assert.True(pixel.Red > 0);
            Assert.True(pixel.Blue > 0);
        }

        [Fact]
        public void MaskHidesPixels()
        {
            var service = new LayerService();
            using var bitmap = CreateSolidBitmap(10, 10, new SKColor(255, 0, 0));
            using var mask = Editor.Models.LayerMask.CreateFilled(10, 10, 0);
            service.Add(bitmap);
            service.Layers[0].SetMask(mask);

            using var result = service.Composite();
            var pixel = result.GetPixel(5, 5);

            Assert.Equal(0, pixel.Alpha);
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            var service = new LayerService();
            using var bitmap = new SKBitmap(10, 10);
            service.Add(bitmap);

            service.Dispose();

            Assert.Equal(0, service.Count);
        }

        private static SKBitmap CreateSolidBitmap(int width, int height, SKColor color)
        {
            var bitmap = new SKBitmap(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bitmap.SetPixel(x, y, color);
            return bitmap;
        }
    }
}
