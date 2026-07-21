using System.Globalization;
using SkiaSharp;

namespace Editor.Services
{
    public class CommandService
    {
        private static readonly string[] HelpLines =
        [
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
        ];

        private static readonly IReadOnlyList<string> HelpText = HelpLines.ToList().AsReadOnly();

        public IReadOnlyList<string> AvailableCommands => HelpText;

        public IReadOnlyList<string> GetHelp() => HelpText;

        public static IReadOnlyList<string> GetHelpText() => HelpText;

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
                    return CommandResult.Ok(string.Join(Environment.NewLine, HelpText));

                case "яркость":
                case "brightness":
                    return ParseBrightness(args, filterService, active);

                case "контраст":
                case "contrast":
                    return ParseContrast(args, filterService, active);

                case "эрозия":
                case "erosion":
                    return CommandExecutor.ExecuteMorphology(MorphologyType.Erode, filterService, active);

                case "дилатация":
                case "dilate":
                    return CommandExecutor.ExecuteMorphology(MorphologyType.Dilate, filterService, active);

                case "открытие":
                case "open":
                    return CommandExecutor.ExecuteMorphology(MorphologyType.Open, filterService, active);

                case "закрытие":
                case "close":
                    return CommandExecutor.ExecuteMorphology(MorphologyType.Close, filterService, active);

                case "удалить":
                case "remove":
                    return CommandExecutor.ExecuteRemove(removeObjectFunc, active);

                case "выделить":
                case "select":
                    return ParseSelect(args, selectFunc, active);

                case "найти":
                case "search":
                    return ParseTextSearch(args, textSearchFunc, active);

                default:
                    return CommandResult.Fail($"Неизвестная команда: {command}. Введите \"помощь\" для списка.");
            }
        }

        private static CommandResult ParseBrightness(string[] args, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return CommandResult.Fail("Использование: яркость <значение> (-255..255)");

            return CommandExecutor.ExecuteBrightness(value, filterService, active);
        }

        private static CommandResult ParseContrast(string[] args, ImageFilterService filterService, Editor.Models.Layer? active)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return CommandResult.Fail("Использование: контраст <значение> (-100..100)");

            return CommandExecutor.ExecuteContrast(value, filterService, active);
        }

        private static CommandResult ParseSelect(string[] args,
            Func<SKBitmap, float, float, SKBitmap?>? selectFunc,
            Editor.Models.Layer? active)
        {
            if (args.Length < 2
                || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return CommandResult.Fail("Использование: выделить <x> <y>");

            return CommandExecutor.ExecuteSelect(x, y, selectFunc, active);
        }

        private static CommandResult ParseTextSearch(string[] args,
            Func<SKBitmap, string, SKBitmap?>? textSearchFunc,
            Editor.Models.Layer? active)
        {
            if (args.Length < 1)
                return CommandResult.Fail("Использование: найти <текстовое описание>");

            var query = string.Join(" ", args);
            return CommandExecutor.ExecuteTextSearch(query, textSearchFunc, active);
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
