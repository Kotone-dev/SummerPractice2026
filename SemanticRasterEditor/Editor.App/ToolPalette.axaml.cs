using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Editor.App
{
    public partial class ToolPalette : UserControl
    {
        private const string SelectedClass = "selected";

        public event Action<string>? ToolSelected;

        public string CurrentTool { get; private set; } = "Move";

        public ToolPalette()
        {
            InitializeComponent();
            BtnMove.Click += (_, _) => SelectTool("Move", BtnMove);
            BtnMarquee.Click += (_, _) => SelectTool("Marquee", BtnMarquee);
            BtnLasso.Click += (_, _) => SelectTool("Lasso", BtnLasso);
            BtnBrush.Click += (_, _) => SelectTool("Brush", BtnBrush);
            BtnEraser.Click += (_, _) => SelectTool("Eraser", BtnEraser);
            BtnText.Click += (_, _) => SelectTool("Text", BtnText);
            BtnEyedropper.Click += (_, _) => SelectTool("Eyedropper", BtnEyedropper);
            BtnCrop.Click += (_, _) => SelectTool("Crop", BtnCrop);
            BtnHand.Click += (_, _) => SelectTool("Hand", BtnHand);
            BtnZoom.Click += (_, _) => SelectTool("Zoom", BtnZoom);
            BtnSmartSelect.Click += (_, _) => SelectTool("SmartSelect", BtnSmartSelect);

            SelectTool("Move", BtnMove);
        }

        public void SelectTool(string tool)
        {
            Button? button = tool switch
            {
                "Move" => BtnMove,
                "Marquee" => BtnMarquee,
                "Lasso" => BtnLasso,
                "Brush" => BtnBrush,
                "Eraser" => BtnEraser,
                "Text" => BtnText,
                "Eyedropper" => BtnEyedropper,
                "Crop" => BtnCrop,
                "Hand" => BtnHand,
                "Zoom" => BtnZoom,
                "SmartSelect" => BtnSmartSelect,
                _ => null
            };
            if (button != null)
                SelectTool(tool, button);
        }

        private void SelectTool(string tool, Button button)
        {
            if (Content is ScrollViewer sv && sv.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is Button btn)
                        btn.Classes.Remove(SelectedClass);
                }
            }

            button.Classes.Add(SelectedClass);
            CurrentTool = tool;
            ToolSelected?.Invoke(tool);
        }
    }
}
