using SkiaSharp;

namespace Editor.Services
{
    public static class CommandExecutor
    {
        public static CommandResult ExecuteBrightness(int value, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            value = Math.Clamp(value, -255, 255);
            var result = filterService.AdjustBrightness(active.Bitmap, value);
            active.SetBitmap(result);
            return CommandResult.Ok($"Яркость изменена на {value}");
        }

        public static CommandResult ExecuteContrast(int value, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            value = Math.Clamp(value, -100, 100);
            var result = filterService.AdjustContrast(active.Bitmap, value);
            active.SetBitmap(result);
            return CommandResult.Ok($"Контраст изменён на {value}");
        }

        public static CommandResult ExecuteMorphology(MorphologyType type, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            var result = filterService.ApplyMorphology(active.Bitmap, type);
            active.SetBitmap(result);
            var name = type switch
            {
                MorphologyType.Erode => "Эрозия",
                MorphologyType.Dilate => "Дилатация",
                MorphologyType.Open => "Открытие",
                MorphologyType.Close => "Закрытие",
                _ => type.ToString()
            };
            return CommandResult.Ok($"{name} применена");
        }

        public static CommandResult ExecuteSelect(float x, float y,
            Func<SKBitmap, float, float, SKBitmap?>? selectFunc,
            Editor.Models.Layer? active)
        {
            if (selectFunc is null)
                return CommandResult.Fail("Функция выделения не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            var mask = selectFunc(active.Bitmap, x, y);
            if (mask is null)
                return CommandResult.Fail("Не удалось выделить объект");

            active.SetMask(mask);
            return CommandResult.Ok($"Объект выделен в ({(int)x}, {(int)y})");
        }

        public static CommandResult ExecuteRemove(
            Func<SKBitmap?, SKBitmap?, SKBitmap?>? removeObjectFunc,
            Editor.Models.Layer? active)
        {
            if (removeObjectFunc is null)
                return CommandResult.Fail("Функция удаления объектов не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (!active.HasMask)
                return CommandResult.Fail("Сначала выделите объект");

            var result = removeObjectFunc(active.Bitmap, active.Mask);
            if (result is null)
                return CommandResult.Fail("Ошибка при удалении объекта");

            active.SetBitmap(result);
            active.ClearMask();
            return CommandResult.Ok("Объект удалён");
        }

        public static CommandResult ExecuteTextSearch(string query,
            Func<SKBitmap, string, SKBitmap?>? textSearchFunc,
            Editor.Models.Layer? active)
        {
            if (textSearchFunc is null)
                return CommandResult.Fail("Функция текстового поиска не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (string.IsNullOrWhiteSpace(query))
                return CommandResult.Fail("Текст запроса не может быть пустым");

            var mask = textSearchFunc(active.Bitmap, query);
            if (mask is null)
                return CommandResult.Fail($"Объект \"{query}\" не найден");

            active.SetMask(mask);
            return CommandResult.Ok($"Объект \"{query}\" найден. Маска применена.");
        }
    }
}
