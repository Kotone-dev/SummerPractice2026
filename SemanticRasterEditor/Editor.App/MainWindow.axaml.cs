using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Editor.Services;

namespace Editor.App
{
    public partial class MainWindow : Window
    {
        private readonly FileService _fileService = new();
        private readonly ImageFilterService _filterService = new();
        private readonly LayerService _layerService = new();
        private SamService? _samService;
        private bool _smartSelectActive;

        public MainWindow()
        {
            InitializeComponent();
            SetupMenu();
            EditorCanvas.ZoomChanged += OnZoomChanged;
            EditorCanvas.ClickOnImage += OnImageClicked;
            Layers.Bind(_layerService);
            Layers.LayerChanged += OnLayerChanged;
            BtnSmartSelect.Click += OnSmartSelectClick;
            KeyDown += OnKeyDown;
        }

        private void SetupMenu()
        {
            var fileMenu = this.FindControl<MenuItem>("MenuFile")!;
            fileMenu.Items.Add(CreateMenuItem("Открыть...", OnOpenClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Сохранить", OnSaveClick));
            fileMenu.Items.Add(CreateMenuItem("Сохранить как...", OnSaveAsClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Выход", OnExitClick));

            var viewMenu = this.FindControl<MenuItem>("MenuView")!;
            viewMenu.Items.Add(CreateMenuItem("Вписать в окно", OnFitToWindowClick));
            viewMenu.Items.Add(CreateMenuItem("Сбросить зум", OnResetZoomClick));

            var correctionMenu = this.FindControl<MenuItem>("MenuCorrection")!;
            correctionMenu.Items.Add(CreateMenuItem("Яркость/Контраст...", OnBrightnessContrastClick));
            correctionMenu.Items.Add(new Separator());
            correctionMenu.Items.Add(CreateMenuItem("Эрозия", OnErodeClick));
            correctionMenu.Items.Add(CreateMenuItem("Дилатация", OnDilateClick));
            correctionMenu.Items.Add(CreateMenuItem("Открытие", OnOpenMorphologyClick));
            correctionMenu.Items.Add(CreateMenuItem("Закрытие", OnCloseMorphologyClick));
        }

        private static MenuItem CreateMenuItem(string header, EventHandler<RoutedEventArgs> handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage is null)
                return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть изображение",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Изображения")
                    {
                        Patterns = FileService.SupportedExtensions
                            .Select(ext => $"*{ext}")
                            .ToArray()
                    }
                }
            });

            if (files.Count == 0)
                return;

            var path = files[0].TryGetLocalPath();
            if (path is null)
                return;

            var bitmap = _fileService.OpenImage(path);
            if (bitmap is null)
                return;

            _layerService.Dispose();
            _layerService.Add(bitmap, Path.GetFileName(path));

            Title = $"SemanticRasterEditor — {Path.GetFileName(path)}";
            RefreshCanvas();
            Layers.Refresh();
            UpdateStatusBar();
            EditorCanvas.FitToWindow();
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService.Count == 0)
                return;

            var filePath = GetCurrentFilePath();
            if (filePath is not null)
            {
                using var composite = _layerService.Composite();
                _fileService.SaveImage(composite, filePath);
                UpdateTitle();
                return;
            }

            await SaveAs();
        }

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        {
            await SaveAs();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnFitToWindowClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.FitToWindow();
            UpdateStatusBar();
        }

        private void OnResetZoomClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.ResetZoom();
            UpdateStatusBar();
        }

        private async void OnBrightnessContrastClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null)
                return;

            var dialog = new FilterDialog();
            var result = await dialog.ShowDialog<bool?>(this);

            if (result != true)
                return;

            var newBitmap = _filterService.AdjustBrightness(active.Bitmap, dialog.Brightness);
            var contrastBitmap = _filterService.AdjustContrast(newBitmap, dialog.Contrast);
            newBitmap.Dispose();

            active.SetBitmap(contrastBitmap);
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnErodeClick(object? sender, RoutedEventArgs e)
        {
            ApplyMorphology(MorphologyType.Erode);
        }

        private void OnDilateClick(object? sender, RoutedEventArgs e)
        {
            ApplyMorphology(MorphologyType.Dilate);
        }

        private void OnOpenMorphologyClick(object? sender, RoutedEventArgs e)
        {
            ApplyMorphology(MorphologyType.Open);
        }

        private void OnCloseMorphologyClick(object? sender, RoutedEventArgs e)
        {
            ApplyMorphology(MorphologyType.Close);
        }

        private void ApplyMorphology(MorphologyType type)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null)
                return;

            var newBitmap = _filterService.ApplyMorphology(active.Bitmap, type);
            active.SetBitmap(newBitmap);
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnSmartSelectClick(object? sender, RoutedEventArgs e)
        {
            _smartSelectActive = !_smartSelectActive;
            EditorCanvas.SmartSelectMode = _smartSelectActive;
            BtnSmartSelect.Content = _smartSelectActive
                ? "Выделение (SAM) [АКТИВНО]"
                : "Выделение (SAM)";
        }

        private void OnImageClicked(float pixelX, float pixelY)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null)
                return;

            EnsureSamService();
            if (_samService is null)
                return;

            var mask = _samService.Predict(active.Bitmap, pixelX, pixelY);
            active.SetMask(mask);
            RefreshCanvas();
        }

        private void EnsureSamService()
        {
            if (_samService is not null)
                return;

            var modelsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Models");
            modelsDir = Path.GetFullPath(modelsDir);

            if (!Directory.Exists(modelsDir))
                return;

            try
            {
                _samService = SamService.LoadFromDirectory(modelsDir);
            }
            catch
            {
                _samService = null;
            }
        }

        private void OnLayerChanged()
        {
            RefreshCanvas();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (ctrl && shift && e.Key == Key.S)
            {
                OnSaveAsClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                OnSaveClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.O)
            {
                OnOpenClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async Task SaveAs()
        {
            if (_layerService.Count == 0)
                return;

            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage is null)
                return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить изображение",
                SuggestedFileName = Path.GetFileName(GetCurrentFilePath() ?? "image.png"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG")
                    {
                        Patterns = new[] { "*.png" }
                    },
                    new FilePickerFileType("JPEG")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg" }
                    },
                    new FilePickerFileType("BMP")
                    {
                        Patterns = new[] { "*.bmp" }
                    }
                }
            });

            if (file is null)
                return;

            var path = file.TryGetLocalPath();
            if (path is null)
                return;

            using var composite = _layerService.Composite();
            _fileService.SaveImage(composite, path);
            UpdateTitle();
        }

        private string? GetCurrentFilePath()
        {
            if (_layerService.Count > 0 && _layerService.Layers[0].Bitmap is not null)
                return null;
            return null;
        }

        private void RefreshCanvas()
        {
            if (_layerService.Count == 0)
            {
                EditorCanvas.SetImage(null);
                return;
            }

            using var composite = _layerService.Composite();
            EditorCanvas.SetImage(composite);
            UpdateStatusBar();
        }

        private void OnZoomChanged(float zoom)
        {
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            var zoomPercent = (int)(EditorCanvas.Zoom * 100);
            ZoomText.Text = $"{zoomPercent}%";

            if (_layerService.Count > 0 && _layerService.ActiveLayer?.Bitmap is not null)
            {
                var bmp = _layerService.ActiveLayer.Bitmap;
                SizeText.Text = $"{bmp.Width} × {bmp.Height}";
            }
            else
                SizeText.Text = string.Empty;
        }

        private void UpdateTitle()
        {
            var fileName = _layerService.Count > 0 && _layerService.Layers[0].Bitmap is not null
                ? _layerService.Layers[0].Name
                : "Без имени";

            Title = $"SemanticRasterEditor — {fileName}";
        }
    }
}
