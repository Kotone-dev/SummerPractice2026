using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Editor.App
{
    public partial class GaussianBlurDialog : Window
    {
        public int Radius { get; private set; }
        public bool DialogResult { get; private set; }

        public GaussianBlurDialog()
        {
            InitializeComponent();
            RadiusSlider.ValueChanged += (_, e) =>
            {
                if (RadiusText is not null)
                    RadiusText.Text = ((int)e.NewValue).ToString();
            };

            BtnApply.Click += (_, _) =>
            {
                Radius = (int)RadiusSlider.Value;
                DialogResult = true;
                Close();
            };
            BtnCancel.Click += (_, _) => Close();
        }
    }
}
