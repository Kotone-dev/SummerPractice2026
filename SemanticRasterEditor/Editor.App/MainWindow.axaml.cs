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
        private LaMaService? _lamaService;
        private TextSearchService? _textSearchService;
        private bool _smartSelectActive;

        public MainWindow()
        {
            InitializeComponent();
            SetupMenus();
            EditorCanvas.ZoomChanged += OnZoomChanged;
            EditorCanvas.ClickOnImage += OnImageClicked;
            EditorCanvas.CursorPositionChanged += OnCursorPositionChanged;
            Layers.Bind(_layerService);
            Layers.LayerChanged += OnLayerChanged;
            ToolPalette.ToolSelected += OnToolSelected;
            KeyDown += OnKeyDown;
            Closing += OnClosing;
            AiChat.TextSearchRequested += OnTextSearchRequested;
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _samService?.Dispose();
            _lamaService?.Dispose();
            _textSearchService?.Dispose();
            _layerService.Dispose();
        }

        private void SetupMenus()
        {
            var fileMenu = this.FindControl<MenuItem>("MenuFile")!;
            fileMenu.Items.Add(CreateMenuItem("Открыть...", OnOpenClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Сохранить", OnSaveClick));
            fileMenu.Items.Add(CreateMenuItem("Сохранить как...", OnSaveAsClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Выход", OnExitClick));

            var editMenu = this.FindControl<MenuItem>("MenuEdit")!;
            editMenu.Items.Add(CreateMenuItem("Отменить", OnUndoClick));
            editMenu.Items.Add(CreateMenuItem("Повторить", OnRedoClick));
            editMenu.Items.Add(new Separator());
            editMenu.Items.Add(CreateMenuItem("Вырезать", OnCutClick));
            editMenu.Items.Add(CreateMenuItem("Копировать", OnCopyClick));
            editMenu.Items.Add(CreateMenuItem("Вставить", OnPasteClick));

            var viewMenu = this.FindControl<MenuItem>("MenuView")!;
            viewMenu.Items.Add(CreateMenuItem("Вписать в окно", OnFitToWindowClick));
            viewMenu.Items.Add(CreateMenuItem("Сбросить зум", OnResetZoomClick));

            var imageMenu = this.FindControl<MenuItem>("MenuImage")!;
            imageMenu.Items.Add(CreateMenuItem("Размер изображения...", OnImageSizeClick));
            imageMenu.Items.Add(CreateMenuItem("Размер холста...", OnCanvasSizeClick));
            imageMenu.Items.Add(new Separator());
            imageMenu.Items.Add(CreateMenuItem("Повернуть вправо", OnRotateRightClick));
            imageMenu.Items.Add(CreateMenuItem("Повернуть влево", OnRotateLeftClick));
            imageMenu.Items.Add(CreateMenuItem("Отразить по горизонтали", OnFlipHorizontalClick));
            imageMenu.Items.Add(CreateMenuItem("Отразить по вертикали", OnFlipVerticalClick));

            var layerMenu = this.FindControl<MenuItem>("MenuLayer")!;
            layerMenu.Items.Add(CreateMenuItem("Новый слой", OnNewLayerClick));
            layerMenu.Items.Add(CreateMenuItem("Дублировать слой", OnDuplicateLayerClick));
            layerMenu.Items.Add(CreateMenuItem("Удалить слой", OnDeleteLayerClick));
            layerMenu.Items.Add(new Separator());
            layerMenu.Items.Add(CreateMenuItem("Объединить все", OnFlattenClick));

            var selectMenu = this.FindControl<MenuItem>("MenuSelect")!;
            selectMenu.Items.Add(CreateMenuItem("Выделить все", OnSelectAllClick));
            selectMenu.Items.Add(CreateMenuItem("Снять выделение", OnDeselectClick));
            selectMenu.Items.Add(CreateMenuItem("Инвертировать выделение", OnInvertSelectionClick));

            var filtersMenu = this.FindControl<MenuItem>("MenuFilters")!;
            filtersMenu.Items.Add(CreateMenuItem("Яркость/Контраст...", OnBrightnessContrastClick));
            filtersMenu.Items.Add(new Separator());
            filtersMenu.Items.Add(CreateMenuItem("Эрозия", OnErodeClick));
            filtersMenu.Items.Add(CreateMenuItem("Дилатация", OnDilateClick));
            filtersMenu.Items.Add(CreateMenuItem("Открытие", OnOpenMorphologyClick));
            filtersMenu.Items.Add(CreateMenuItem("Закрытие", OnCloseMorphologyClick));
            filtersMenu.Items.Add(new Separator());
            filtersMenu.Items.Add(CreateMenuItem("Размытие по Гауссу...", OnGaussianBlurClick));
            filtersMenu.Items.Add(CreateMenuItem("Резкость...", OnSharpenClick));
            filtersMenu.Items.Add(new Separator());
            filtersMenu.Items.Add(CreateMenuItem("Удалить объект", OnRemoveObjectClick));

            var helpMenu = this.FindControl<MenuItem>("MenuHelp")!;
            helpMenu.Items.Add(CreateMenuItem("О программе", OnAboutClick));
        }

        private static MenuItem CreateMenuItem(string header, EventHandler<RoutedEventArgs> handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        #region File menu

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            try
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

                Title = $"RasterEditor — {Path.GetFileName(path)}";
                RefreshCanvas();
                Layers.Refresh();
                UpdateStatusBar();
                UpdateDocumentTab();
                EditorCanvas.FitToWindow();
                UpdateRulers();
            }
            catch
            {
            }
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            try
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
            catch
            {
            }
        }

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                await SaveAs();
            }
            catch
            {
            }
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Edit menu

        private void OnUndoClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnRedoClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnCutClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnPasteClick(object? sender, RoutedEventArgs e)
        {
        }

        #endregion

        #region View menu

        private void OnFitToWindowClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.FitToWindow();
            UpdateStatusBar();
            UpdateRulers();
        }

        private void OnResetZoomClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.ResetZoom();
            UpdateStatusBar();
            UpdateRulers();
        }

        #endregion

        #region Image menu

        private void OnImageSizeClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnCanvasSizeClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnRotateRightClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnRotateLeftClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnFlipVerticalClick(object? sender, RoutedEventArgs e)
        {
        }

        #endregion

        #region Layer menu

        private void OnNewLayerClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnDuplicateLayerClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnDeleteLayerClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnFlattenClick(object? sender, RoutedEventArgs e)
        {
        }

        #endregion

        #region Select menu

        private void OnSelectAllClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnDeselectClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnInvertSelectionClick(object? sender, RoutedEventArgs e)
        {
        }

        #endregion

        #region Filters menu

        private async void OnBrightnessContrastClick(object? sender, RoutedEventArgs e)
        {
            try
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
            catch
            {
            }
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

        private void OnGaussianBlurClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnSharpenClick(object? sender, RoutedEventArgs e)
        {
        }

        private void OnRemoveObjectClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var active = _layerService.ActiveLayer;
                if (active?.Bitmap is null)
                    return;

                if (!active.HasMask)
                    return;

                EnsureLaMaService();
                if (_lamaService is null)
                    return;

                using var mask = active.Mask!;
                var result = _lamaService.Inpaint(active.Bitmap, mask);
                active.SetBitmap(result);
                active.ClearMask();
                RefreshCanvas();
                UpdateTitle();
            }
            catch
            {
            }
        }

        #endregion

        #region Help menu

        private void OnAboutClick(object? sender, RoutedEventArgs e)
        {
        }

        #endregion

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

        private void OnToolSelected(string tool)
        {
            if (tool == "SmartSelect")
            {
                _smartSelectActive = !_smartSelectActive;
                EditorCanvas.SmartSelectMode = _smartSelectActive;
            }
            else
            {
                _smartSelectActive = false;
                EditorCanvas.SmartSelectMode = false;
            }
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

        private void EnsureLaMaService()
        {
            if (_lamaService is not null)
                return;

            var modelsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Models");
            modelsDir = Path.GetFullPath(modelsDir);

            if (!Directory.Exists(modelsDir))
                return;

            try
            {
                _lamaService = LaMaService.LoadFromDirectory(modelsDir);
            }
            catch
            {
                _lamaService = null;
            }
        }

        private void EnsureTextSearchService()
        {
            if (_textSearchService is not null)
                return;

            var modelsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Models");
            modelsDir = Path.GetFullPath(modelsDir);

            if (!Directory.Exists(modelsDir))
                return;

            try
            {
                _textSearchService = TextSearchService.LoadFromDirectory(modelsDir);
            }
            catch
            {
                _textSearchService = null;
            }
        }

        private async void OnTextSearchRequested(object? sender, string query)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null)
            {
                AiChat.AddMessage("Нет загруженного изображения.");
                return;
            }

            EnsureTextSearchService();
            if (_textSearchService is null)
            {
                AiChat.AddMessage("Модели поиска не загружены. Проверьте папку Assets/Models.");
                return;
            }

            AiChat.ShowProgress("Поиск объекта");
            AiChat.AddMessage($"Ищу: \"{query}\"", true);

            try
            {
                var bitmap = active.Bitmap;
                var mask = await Task.Run(() => _textSearchService.Search(bitmap, query));

                if (mask is null)
                {
                    AiChat.AddMessage("Объект не найден.");
                }
                else
                {
                    active.SetMask(mask);
                    RefreshCanvas();
                    AiChat.AddMessage("Объект найден. Маска применена.");
                }
            }
            catch (Exception ex)
            {
                AiChat.AddMessage($"Ошибка: {ex.Message}");
            }
            finally
            {
                AiChat.HideProgress();
            }
        }

        private void OnCursorPositionChanged(float x, float y)
        {
            if (CursorPosText is not null)
                CursorPosText.Text = $"X: {(int)x}  Y: {(int)y}";
            if (RulerH is not null)
                RulerH.CursorPosition = x;
            if (RulerV is not null)
                RulerV.CursorPosition = y;
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
                    new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("BMP") { Patterns = new[] { "*.bmp" } }
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
            return null;
        }

        private void RefreshCanvas()
        {
            if (_layerService.Count == 0)
            {
                EditorCanvas.SetImage(null);
                Layers.Channels?.SetImage(null);
                return;
            }

            using var composite = _layerService.Composite();
            EditorCanvas.SetImage(composite);
            Layers.Channels?.SetImage(_layerService.ActiveLayer?.Bitmap);
            UpdateStatusBar();
            UpdateDocumentTab();
        }

        private void OnZoomChanged(float zoom)
        {
            UpdateStatusBar();
            UpdateDocumentTab();
            UpdateRulers();
        }

        private void UpdateStatusBar()
        {
            var zoomPercent = (int)(EditorCanvas.Zoom * 100);
            if (ZoomText is not null)
                ZoomText.Text = $"{zoomPercent}%";

            if (_layerService.Count > 0 && _layerService.ActiveLayer?.Bitmap is not null)
            {
                var bmp = _layerService.ActiveLayer.Bitmap;
                if (SizeText is not null)
                    SizeText.Text = $"{bmp.Width} x {bmp.Height} px";
                if (PpiText is not null)
                    PpiText.Text = "RGB/8";
            }
            else
            {
                if (SizeText is not null)
                    SizeText.Text = string.Empty;
                if (PpiText is not null)
                    PpiText.Text = string.Empty;
            }
        }

        private void UpdateDocumentTab()
        {
            var fileName = _layerService.Count > 0 && _layerService.Layers[0].Bitmap is not null
                ? _layerService.Layers[0].Name
                : "Без имени";

            DocTab?.UpdateInfo(fileName, EditorCanvas.Zoom);
        }

        private void UpdateRulers()
        {
            if (RulerH is not null)
            {
                RulerH.Zoom = EditorCanvas.Zoom;
                RulerH.Offset = EditorCanvas.PanOffset.X;
            }
            if (RulerV is not null)
            {
                RulerV.Zoom = EditorCanvas.Zoom;
                RulerV.Offset = EditorCanvas.PanOffset.Y;
            }
        }

        private void UpdateTitle()
        {
            var fileName = _layerService.Count > 0 && _layerService.Layers[0].Bitmap is not null
                ? _layerService.Layers[0].Name
                : "Без имени";

            Title = $"RasterEditor — {fileName}";
        }
    }
}
