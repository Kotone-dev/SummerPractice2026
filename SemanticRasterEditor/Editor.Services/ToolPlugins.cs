using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Editor.Services
{
    public class ToolPlugins
    {
        [KernelFunction]
        [Description("Удаляет выделенный объект с изображения (LaMa inpainting).")]
        public string RemoveObject() => "Запрос на удаление объекта";

        [KernelFunction]
        [Description("Выделяет объект по координатам (MobileSAM).")]
        public string SelectObject(
            [Description("Координата X")] float x,
            [Description("Координата Y")] float y) => $"Выделение в ({x}, {y})";

        [KernelFunction]
        [Description("Ищет объект на изображении по текстовому описанию (FastSAM + ruCLIP).")]
        public string SearchByText(
            [Description("Текстовое описание объекта")] string query) => $"Поиск: {query}";

        [KernelFunction]
        [Description("Показывает список доступных команд.")]
        public string ShowHelp() => "Команды: яркость, контраст, эрозия, дилатация, удалить, выделить, найти";
    }
}
