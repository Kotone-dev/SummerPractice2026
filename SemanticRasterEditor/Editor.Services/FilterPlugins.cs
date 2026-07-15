using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Editor.Services
{
    public class FilterPlugins
    {
        private readonly ImageFilterService _filterService = new();

        [KernelFunction]
        [Description("Изменяет яркость изображения. Значение от -255 до 255.")]
        public string AdjustBrightness([Description("Значение яркости")] int value)
        {
            value = Math.Clamp(value, -255, 255);
            return $"Яркость: {value}";
        }

        [KernelFunction]
        [Description("Изменяет контраст изображения. Значение от -100 до 100.")]
        public string AdjustContrast([Description("Значение контраста")] int value)
        {
            value = Math.Clamp(value, -100, 100);
            return $"Контраст: {value}";
        }

        [KernelFunction]
        [Description("Применяет эрозию к изображению.")]
        public string ApplyErosion() => "Эрозия применена";

        [KernelFunction]
        [Description("Применяет дилатацию к изображению.")]
        public string ApplyDilation() => "Дилатация применена";

        [KernelFunction]
        [Description("Применяет морфологическое открытие к изображению.")]
        public string ApplyMorphOpen() => "Морфологическое открытие применено";

        [KernelFunction]
        [Description("Применяет морфологическое закрытие к изображению.")]
        public string ApplyMorphClose() => "Морфологическое закрытие применено";

        public ImageFilterService GetFilterService() => _filterService;
    }
}
