using Editor.Services;
using Editor.Models;
using SkiaSharp;

namespace Tests.Unit
{
    public class CommandServiceTests
    {
        [Fact]
        public void Execute_EmptyCommand_ReturnsFail()
        {
            var result = CommandService.ParseAndExecute("", new ImageFilterService(), CreateLayerService());
            Assert.False(result.Success);
        }

        [Fact]
        public void Execute_WhitespaceCommand_ReturnsFail()
        {
            var result = CommandService.ParseAndExecute("   ", new ImageFilterService(), CreateLayerService());
            Assert.False(result.Success);
        }

        [Fact]
        public void Execute_UnknownCommand_ReturnsFail()
        {
            var result = CommandService.ParseAndExecute("未知", new ImageFilterService(), CreateLayerService());
            Assert.False(result.Success);
            Assert.Contains("Неизвестная команда", result.Message);
        }

        [Fact]
        public void Execute_Help_ReturnsSuccess()
        {
            var result = CommandService.ParseAndExecute("помощь", new ImageFilterService(), CreateLayerService());
            Assert.True(result.Success);
            Assert.Contains("яркость", result.Message);
        }

        [Fact]
        public void Execute_HelpEnglish_ReturnsSuccess()
        {
            var result = CommandService.ParseAndExecute("help", new ImageFilterService(), CreateLayerService());
            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_BrightnessNoArgs_ReturnsFail()
        {
            var result = CommandService.ParseAndExecute("яркость", new ImageFilterService(), CreateLayerService());
            Assert.False(result.Success);
            Assert.Contains("Использование", result.Message);
        }

        [Fact]
        public void Execute_BrightnessInvalidArg_ReturnsFail()
        {
            var result = CommandService.ParseAndExecute("яркость abc", new ImageFilterService(), CreateLayerService());
            Assert.False(result.Success);
        }

        [Fact]
        public void Execute_BrightnessNoLayer_ReturnsFail()
        {
            var filterService = new ImageFilterService();
            var layerService = CreateLayerService();
            var result = CommandService.ParseAndExecute("яркость 50", filterService, layerService);
            Assert.False(result.Success);
            Assert.Contains("Нет активного слоя", result.Message);
        }

        [Fact]
        public void Execute_BrightnessWithLayer_AppliesFilter()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            var filterService = new ImageFilterService();

            var result = CommandService.ParseAndExecute("яркость 50", filterService, layerService);

            Assert.True(result.Success);
            Assert.Contains("Яркость", result.Message);
        }

        [Fact]
        public void Execute_BrightnessEnglish_AppliesFilter()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            var filterService = new ImageFilterService();

            var result = CommandService.ParseAndExecute("brightness 50", filterService, layerService);

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_ContrastWithLayer_AppliesFilter()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            var filterService = new ImageFilterService();

            var result = CommandService.ParseAndExecute("контраст 50", filterService, layerService);

            Assert.True(result.Success);
            Assert.Contains("Контраст", result.Message);
        }

        [Fact]
        public void Execute_ErodeWithLayer_AppliesFilter()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(200, 200, 200));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            var filterService = new ImageFilterService();

            var result = CommandService.ParseAndExecute("эрозия", filterService, layerService);

            Assert.True(result.Success);
            Assert.Contains("Эрозия", result.Message);
        }

        [Fact]
        public void Execute_DilateWithLayer_AppliesFilter()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(200, 200, 200));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            var filterService = new ImageFilterService();

            var result = CommandService.ParseAndExecute("дилатация", filterService, layerService);

            Assert.True(result.Success);
            Assert.Contains("Дилатация", result.Message);
        }

        [Fact]
        public void Execute_RemoveNoMask_ReturnsFail()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            var result = CommandService.ParseAndExecute("удалить", new ImageFilterService(), layerService,
                removeObjectFunc: (_, _) => null);

            Assert.False(result.Success);
            Assert.Contains("выделите объект", result.Message);
        }

        [Fact]
        public void Execute_RemoveWithMask_CallsFunc()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            using var mask = CreateTestBitmap(10, 10, new SKColor(255, 0, 0, 255));
            var layerService = CreateLayerServiceWithBitmap(bitmap);
            layerService.ActiveLayer!.SetMask(mask);

            bool called = false;
            var result = CommandService.ParseAndExecute("удалить", new ImageFilterService(), layerService,
                removeObjectFunc: (_, _) => { called = true; return CreateTestBitmap(10, 10, new SKColor(0, 0, 0)); });

            Assert.True(result.Success);
            Assert.True(called);
        }

        [Fact]
        public void Execute_SelectNoArgs_ReturnsFail()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            var result = CommandService.ParseAndExecute("выделить", new ImageFilterService(), layerService,
                selectFunc: (_, _, _) => null);

            Assert.False(result.Success);
            Assert.Contains("Использование", result.Message);
        }

        [Fact]
        public void Execute_SelectWithCoords_CallsFunc()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            bool called = false;
            float capturedX = 0, capturedY = 0;
            var result = CommandService.ParseAndExecute("выделить 5.5 3.2", new ImageFilterService(), layerService,
                selectFunc: (bmp, x, y) => { called = true; capturedX = x; capturedY = y; return CreateTestBitmap(10, 10, new SKColor(255, 0, 0, 255)); });

            Assert.True(result.Success);
            Assert.True(called);
            Assert.Equal(5.5f, capturedX);
            Assert.Equal(3.2f, capturedY);
        }

        [Fact]
        public void Execute_SearchNoArgs_ReturnsFail()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            var result = CommandService.ParseAndExecute("найти", new ImageFilterService(), layerService,
                textSearchFunc: (_, _) => null);

            Assert.False(result.Success);
            Assert.Contains("Использование", result.Message);
        }

        [Fact]
        public void Execute_SearchWithQuery_CallsFunc()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            bool called = false;
            string capturedQuery = "";
            var result = CommandService.ParseAndExecute("найти красный дом", new ImageFilterService(), layerService,
                textSearchFunc: (bmp, q) => { called = true; capturedQuery = q; return CreateTestBitmap(10, 10, new SKColor(255, 0, 0, 255)); });

            Assert.True(result.Success);
            Assert.True(called);
            Assert.Equal("красный дом", capturedQuery);
        }

        [Fact]
        public void Execute_SearchNotFound_ReturnsFail()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));
            var layerService = CreateLayerServiceWithBitmap(bitmap);

            var result = CommandService.ParseAndExecute("найти несуществующий", new ImageFilterService(), layerService,
                textSearchFunc: (_, _) => null);

            Assert.False(result.Success);
            Assert.Contains("не найден", result.Message);
        }

        [Fact]
        public void AvailableCommands_ContainsHelpCommand()
        {
            var result = CommandService.ParseAndExecute("помощь", new ImageFilterService(), CreateLayerService());
            Assert.True(result.Success);
            Assert.Contains("яркость", result.Message);
        }

        [Fact]
        public void GetHelp_ReturnsNonEmptyList()
        {
            var service = new CommandService();
            var help = service.GetHelp();
            Assert.NotEmpty(help);
        }

        private static SKBitmap CreateTestBitmap(int width, int height, SKColor color)
        {
            var bitmap = new SKBitmap(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bitmap.SetPixel(x, y, color);
            return bitmap;
        }

        private static LayerService CreateLayerService()
        {
            return new LayerService();
        }

        private static LayerService CreateLayerServiceWithBitmap(SKBitmap bitmap)
        {
            var service = new LayerService();
            service.Add(bitmap, "Test");
            return service;
        }
    }
}
