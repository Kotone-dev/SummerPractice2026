using Avalonia.Controls;

namespace Editor.App
{
    public partial class DocumentTab : UserControl
    {
        public DocumentTab()
        {
            InitializeComponent();
        }

        public void UpdateInfo(string fileName, float zoom, bool modified = false)
        {
            if (FileNameText is not null)
                FileNameText.Text = fileName;
            if (ZoomText is not null)
                ZoomText.Text = $"{zoom * 100:F1}%";
            if (ModifiedIndicator is not null)
                ModifiedIndicator.IsVisible = modified;
        }
    }
}
