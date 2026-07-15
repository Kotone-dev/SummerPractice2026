using System.Globalization;
using Microsoft.SemanticKernel;
using SkiaSharp;

namespace Editor.Services
{
    public class CommandService
    {
        private readonly Kernel _kernel;
        private readonly Dictionary<string, Func<string[], CommandResult>> _commands;

        public CommandService()
        {
            _kernel = Kernel.CreateBuilder().Build();
            _commands = new Dictionary<string, Func<string[], CommandResult>>(StringComparer.OrdinalIgnoreCase);
            RegisterBuiltInCommands();
        }

        public CommandResult Execute(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return CommandResult.Fail("Пустая команда");

            var parts = commandText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commandName = parts[0];
            var args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(commandName, out var handler))
                return handler(args);

            return CommandResult.Fail($"Неизвестная команда: {commandName}");
        }

        public IReadOnlyList<string> AvailableCommands => _commands.Keys.ToList().AsReadOnly();

        public IReadOnlyList<string> GetHelp()
        {
            var help = new List<string>
            {
                "Доступные команды:",
                "  яркость <значение>      - Изменить яркость (-255..255)",
                "  контраст <значение>      - Изменить контраст (-100..100)",
                "  эрозия                   - Применить эрозию",
                "  дилатация                - Применить дилатацию",
                "  открытие                 - Морфологическое открытие",
                "  закрытие                 - Морфологическое закрытие",
                "  удалить                  - Удалить выделенный объект (LaMa)",
                "  выделить <x> <y>         - Выделить объект по координатам",
                "  найти <текст>            - Найти объект по текстовому описанию",
                "  помощь                   - Показать справку"
            };
            return help;
        }

        public static CommandResult ParseAndExecute(string input, ImageFilterService filterService,
            LayerService layerService, Func<SKBitmap?, SKBitmap?, SKBitmap?>? removeObjectFunc = null,
            Func<SKBitmap, float, float, SKBitmap?>? selectFunc = null,
            Func<SKBitmap, string, SKBitmap?>? textSearchFunc = null)
        {
            if (string.IsNullOrWhiteSpace(input))
                return CommandResult.Fail("Пустая команда");

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            var active = layerService.ActiveLayer;

            switch (command)
            {
                case "помощь":
                case "help":
                    return CommandResult.Ok("Команды: яркость, контраст, эрозия, дилатация, открытие, закрытие, удалить, выделить, найти, помощь");

                case "яркость":
                case "brightness":
                    return ExecuteBrightness(args, filterService, active);

                case "контраст":
                case "contrast":
                    return ExecuteContrast(args, filterService, active);

                case "эрозия":
                case "erosion":
                    return ExecuteMorphology(MorphologyType.Erode, filterService, active);

                case "дилатация":
                case "dilate":
                    return ExecuteMorphology(MorphologyType.Dilate, filterService, active);

                case "открытие":
                case "open":
                    return ExecuteMorphology(MorphologyType.Open, filterService, active);

                case "закрытие":
                case "close":
                    return ExecuteMorphology(MorphologyType.Close, filterService, active);

                case "удалить":
                case "remove":
                    return ExecuteRemoveObject(removeObjectFunc, active);

                case "выделить":
                case "select":
                    return ExecuteSelect(args, selectFunc, active);

                case "найти":
                case "search":
                    return ExecuteTextSearch(args, textSearchFunc, active);

                default:
                    return CommandResult.Fail($"Неизвестная команда: {command}. Введите \"помощь\" для списка.");
            }
        }

        private static CommandResult ExecuteBrightness(string[] args, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return CommandResult.Fail("Использование: яркость <значение> (-255..255)");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            value = Math.Clamp(value, -255, 255);
            var result = filterService.AdjustBrightness(active.Bitmap, value);
            active.SetBitmap(result);
            return CommandResult.Ok($"Яркость изменена на {value}");
        }

        private static CommandResult ExecuteContrast(string[] args, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return CommandResult.Fail("Использование: контраст <значение> (-100..100)");

            value = Math.Clamp(value, -100, 100);
            var result = filterService.AdjustContrast(active.Bitmap, value);
            active.SetBitmap(result);
            return CommandResult.Ok($"Контраст изменён на {value}");
        }

        private static CommandResult ExecuteMorphology(MorphologyType type, ImageFilterService filterService, Editor.Models.Layer? active)
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

        private static CommandResult ExecuteRemoveObject(
            Func<SKBitmap?, SKBitmap?, SKBitmap?>? removeObjectFunc,
            Editor.Models.Layer? active)
        {
            if (removeObjectFunc is null)
                return CommandResult.Fail("Функция удаления объектов не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (!active.HasMask)
                return CommandResult.Fail("Сначала выделите объект (инструмент SmartSelect или команда \"выделить\")");

            var result = removeObjectFunc(active.Bitmap, active.Mask);
            if (result is null)
                return CommandResult.Fail("Ошибка при удалении объекта");

            active.SetBitmap(result);
            active.ClearMask();
            return CommandResult.Ok("Объект удалён");
        }

        private static CommandResult ExecuteSelect(string[] args,
            Func<SKBitmap, float, float, SKBitmap?>? selectFunc,
            Editor.Models.Layer? active)
        {
            if (selectFunc is null)
                return CommandResult.Fail("Функция выделения не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (args.Length < 2
                || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return CommandResult.Fail("Использование: выделить <x> <y>");

            var mask = selectFunc(active.Bitmap, x, y);
            if (mask is null)
                return CommandResult.Fail("Не удалось выделить объект");

            active.SetMask(mask);
            return CommandResult.Ok($"Объект выделен в ({(int)x}, {(int)y})");
        }

        private static CommandResult ExecuteTextSearch(string[] args,
            Func<SKBitmap, string, SKBitmap?>? textSearchFunc,
            Editor.Models.Layer? active)
        {
            if (textSearchFunc is null)
                return CommandResult.Fail("Функция текстового поиска не подключена");

            if (active?.Bitmap is null)
                return CommandResult.Fail("Нет активного слоя с изображением");

            if (args.Length < 1)
                return CommandResult.Fail("Использование: найти <текстовое описание>");

            var query = string.Join(" ", args);
            var mask = textSearchFunc(active.Bitmap, query);
            if (mask is null)
                return CommandResult.Fail($"Объект \"{query}\" не найден");

            active.SetMask(mask);
            return CommandResult.Ok($"Объект \"{query}\" найден. Маска применена.");
        }

        private void RegisterBuiltInCommands()
        {
            _commands["помощь"] = _ => CommandResult.Ok(string.Join(Environment.NewLine, GetHelp()));
            _commands["help"] = _ => CommandResult.Ok(string.Join(Environment.NewLine, GetHelp()));
        }
    }

    public class CommandResult
    {
        public bool Success { get; }
        public string Message { get; }

        private CommandResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static CommandResult Ok(string message) => new(true, message);
        public static CommandResult Fail(string message) => new(false, message);
    }
}
