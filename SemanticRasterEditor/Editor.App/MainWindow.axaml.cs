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
        private readonly ImageHistoryService _historyService = new();
        private string? _currentFilePath;
        private bool _isModified;

        public MainWindow()
        {
            InitializeComponent();
            SetupMenus();

            EditorCanvas.LayerService = _layerService;
            EditorCanvas.FilterService = _filterService;
            EditorCanvas.ZoomChanged += OnZoomChanged;
            EditorCanvas.CursorPositionChanged += OnCursorPositionChanged;
            EditorCanvas.BeforeImageModified += OnBeforeImageModified;
            EditorCanvas.ImageModified += OnCanvasImageModified;
            EditorCanvas.LayerStateChanged += () => Layers.Refresh();

            if (ToolOptions is not null)
            {
                ToolOptions.ColorChanged += (_, c) => EditorCanvas.BrushColor = c;
                EditorCanvas.BrushSize = ToolOptions.BrushSize;
                EditorCanvas.BrushOpacity = (float)(ToolOptions.OpacityValue / 100.0);
                EditorCanvas.ColorPicked += (_, c) =>
                {
                    ToolOptions.BrushColor = c;
                };
                ToolOptions.UndoClicked += (_, _) => OnUndoClick(this, new RoutedEventArgs());
                ToolOptions.RedoClicked += (_, _) => OnRedoClick(this, new RoutedEventArgs());
            }

            Layers.Bind(_layerService);
            Layers.LayerChanged += OnLayerChanged;
            if (Layers.Channels is not null)
                Layers.Channels.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            ToolPalette.ToolSelected += OnToolSelected;
            KeyDown += OnKeyDown;
            Closing += OnClosing;
            UpdateMenuStates();
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_isModified && _layerService.Count > 0)
            {
                e.Cancel = true;

                var storage = GetTopLevel(this)?.StorageProvider;
                if (storage is null)
                    return;

                var dialog = new Window
                {
                    Title = "Сохранить изменения?",
                    Width = 360,
                    Height = 160,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Background = Avalonia.Media.Brushes.Black
                };

                var panel = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12
                };

                panel.Children.Add(new TextBlock
                {
                    Text = "Документ был изменён. Сохранить изменения?",
                    Foreground = Avalonia.Media.Brushes.White,
                    FontSize = 14,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });

                var btnPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8
                };

                var btnSave = new Button
                {
                    Content = "Сохранить",
                    Width = 90,
                    Height = 30,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2563EB")),
                    Foreground = Avalonia.Media.Brushes.White,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                var btnDontSave = new Button
                {
                    Content = "Не сохранять",
                    Width = 110,
                    Height = 30,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3E4249")),
                    Foreground = Avalonia.Media.Brushes.White,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                var btnCancel = new Button
                {
                    Content = "Отмена",
                    Width = 90,
                    Height = 30,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3E4249")),
                    Foreground = Avalonia.Media.Brushes.White,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                bool? saveResult = null;
                btnSave.Click += (_, _) => { saveResult = true; dialog.Close(); };
                btnDontSave.Click += (_, _) => { saveResult = false; dialog.Close(); };
                btnCancel.Click += (_, _) => { dialog.Close(); };

                btnPanel.Children.Add(btnSave);
                btnPanel.Children.Add(btnDontSave);
                btnPanel.Children.Add(btnCancel);
                panel.Children.Add(btnPanel);
                dialog.Content = panel;

                await dialog.ShowDialog(this);

                if (saveResult == true)
                {
                    await SaveAs();
                    if (!_isModified)
                    {
                        e.Cancel = false;
                        return;
                    }
                }
                else if (saveResult == false)
                {
                    e.Cancel = false;
                }
            }

            EditorCanvas.ClipboardBitmap?.Dispose();
            _historyService.Dispose();
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
            editMenu.Items.Add(CreateMenuItem("Отменить (Ctrl+Z)", OnUndoClick, false));
            editMenu.Items.Add(CreateMenuItem("Повторить (Ctrl+Y)", OnRedoClick, false));
            editMenu.Items.Add(new Separator());
            editMenu.Items.Add(CreateMenuItem("Вырезать (Ctrl+X)", OnCutClick, false));
            editMenu.Items.Add(CreateMenuItem("Копировать (Ctrl+C)", OnCopyClick, false));
            editMenu.Items.Add(CreateMenuItem("Вставить (Ctrl+V)", OnPasteClick, false));

            var viewMenu = this.FindControl<MenuItem>("MenuView")!;
            viewMenu.Items.Add(CreateMenuItem("Вписать в окно", OnFitToWindowClick));
            viewMenu.Items.Add(CreateMenuItem("Сбросить зум", OnResetZoomClick));

            var imageMenu = this.FindControl<MenuItem>("MenuImage")!;
            imageMenu.Items.Add(CreateMenuItem("Размер изображения...", OnImageSizeClick, false));
            imageMenu.Items.Add(CreateMenuItem("Размер холста...", OnCanvasSizeClick, false));
            imageMenu.Items.Add(new Separator());
            imageMenu.Items.Add(CreateMenuItem("Повернуть вправо (90°)", OnRotateRightClick, false));
            imageMenu.Items.Add(CreateMenuItem("Повернуть влево (90°)", OnRotateLeftClick, false));
            imageMenu.Items.Add(CreateMenuItem("Отразить по горизонтали", OnFlipHorizontalClick, false));
            imageMenu.Items.Add(CreateMenuItem("Отразить по вертикали", OnFlipVerticalClick, false));

            var layerMenu = this.FindControl<MenuItem>("MenuLayer")!;
            layerMenu.Items.Add(CreateMenuItem("Новый слой", OnNewLayerClick, false));
            layerMenu.Items.Add(CreateMenuItem("Дублировать слой", OnDuplicateLayerClick, false));
            layerMenu.Items.Add(CreateMenuItem("Удалить слой", OnDeleteLayerClick, false));
            layerMenu.Items.Add(new Separator());
            layerMenu.Items.Add(CreateMenuItem("Объединить все (Ctrl+J)", OnFlattenClick, false));

            var selectMenu = this.FindControl<MenuItem>("MenuSelect")!;
            selectMenu.Items.Add(CreateMenuItem("Выделить все (Ctrl+A)", OnSelectAllClick, false));
            selectMenu.Items.Add(CreateMenuItem("Снять выделение (Ctrl+D)", OnDeselectClick, false));
            selectMenu.Items.Add(CreateMenuItem("Инвертировать выделение", OnInvertSelectionClick, false));

            var filtersMenu = this.FindControl<MenuItem>("MenuFilters")!;
            filtersMenu.Items.Add(CreateMenuItem("Яркость/Контраст...", OnBrightnessContrastClick, false));
            filtersMenu.Items.Add(new Separator());
            filtersMenu.Items.Add(CreateMenuItem("Эрозия", OnErodeClick, false));
            filtersMenu.Items.Add(CreateMenuItem("Дилатация", OnDilateClick, false));
            filtersMenu.Items.Add(CreateMenuItem("Открытие", OnOpenMorphologyClick, false));
            filtersMenu.Items.Add(CreateMenuItem("Закрытие", OnCloseMorphologyClick, false));
            filtersMenu.Items.Add(new Separator());
            filtersMenu.Items.Add(CreateMenuItem("Размытие по Гауссу...", OnGaussianBlurClick, false));
            filtersMenu.Items.Add(CreateMenuItem("Резкость", OnSharpenClick, false));

            var helpMenu = this.FindControl<MenuItem>("MenuHelp")!;
            helpMenu.Items.Add(CreateMenuItem("О программе", OnAboutClick));
        }

        private static MenuItem CreateMenuItem(string header, EventHandler<RoutedEventArgs> handler, bool isEnabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = isEnabled };
            item.Click += handler;
            return item;
        }

        #region File menu

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var storage = GetTopLevel(this)?.StorageProvider;
                if (storage is null) return;

                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Открыть изображение",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Изображения")
                        {
                            Patterns = FileService.SupportedExtensions
                                .Select(ext => $"*{ext}").ToArray()
                        }
                    }
                });

                if (files.Count == 0) return;
                var path = files[0].TryGetLocalPath();
                if (path is null) return;
                await OpenFile(path);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка при открытии: {ex.Message}";
            }
        }

        private async Task OpenFile(string path)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bitmap = await Task.Run(() => _fileService.OpenImage(path));
            sw.Stop();
            if (bitmap is null) { StatusText.Text = ""; return; }

            _layerService.Dispose();
            _historyService.Clear();
            _layerService.Add(bitmap, Path.GetFileName(path));
            _currentFilePath = path;
            _isModified = false;

            UpdateTitle();
            EditorCanvas.MarkFitToWindow();
            var swCanvas = System.Diagnostics.Stopwatch.StartNew();
            RefreshCanvas();
            swCanvas.Stop();
            var swLayers = System.Diagnostics.Stopwatch.StartNew();
            Layers.Refresh();
            swLayers.Stop();
            UpdateStatusBar();
            UpdateDocumentTab();
            UpdateMenuStates();

            swTotal.Stop();
            System.Diagnostics.Debug.WriteLine(
                $"[OpenFile] Total={swTotal.ElapsedMilliseconds}ms | Decode={sw.ElapsedMilliseconds}ms | " +
                $"RefreshCanvas={swCanvas.ElapsedMilliseconds}ms | Layers={swLayers.ElapsedMilliseconds}ms | " +
                $"Size={bitmap.Width}x{bitmap.Height}");
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_layerService.Count == 0) return;

                if (_currentFilePath is not null)
                {
                    using var composite = _layerService.Composite();
                    _fileService.SaveImage(composite, _currentFilePath);
                    _isModified = false;
                    UpdateTitle();
                    return;
                }
                await SaveAs();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex}");
                StatusText.Text = $"Ошибка при сохранении: {ex.Message}";
            }
        }

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        {
            try { await SaveAs(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveAs error: {ex}");
                StatusText.Text = $"Ошибка при сохранении: {ex.Message}";
            }
        }

        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

        private async Task SaveAs()
        {
            if (_layerService.Count == 0) return;

            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить изображение",
                SuggestedFileName = "image.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("BMP") { Patterns = new[] { "*.bmp" } }
                }
            });

            if (file is null) return;
            var path = file.TryGetLocalPath();
            if (path is null) return;

            using var composite = _layerService.Composite();
            _fileService.SaveImage(composite, path);
            _currentFilePath = path;
            _isModified = false;
            UpdateTitle();
        }

        #endregion

        #region Edit menu

        private void OnUndoClick(object? sender, RoutedEventArgs e)
        {
            if (!_historyService.CanUndo) return;
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;

            var restored = _historyService.Undo(active.Bitmap);
            if (restored is not null)
            {
                active.SetBitmap(restored);
                _isModified = true;
                RefreshCanvas();
                UpdateTitle();
                UpdateMenuStates();
            }
        }

        private void OnRedoClick(object? sender, RoutedEventArgs e)
        {
            if (!_historyService.CanRedo) return;
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;

            var restored = _historyService.Redo(active.Bitmap);
            if (restored is not null)
            {
                active.SetBitmap(restored);
                _isModified = true;
                RefreshCanvas();
                UpdateTitle();
                UpdateMenuStates();
            }
        }

        private void OnCutClick(object? sender, RoutedEventArgs e)
        {
            var selBitmap = EditorCanvas.GetSelectionBitmap();
            if (selBitmap is null) return;

            EditorCanvas.ClipboardBitmap?.Dispose();
            EditorCanvas.ClipboardBitmap = selBitmap;
            EditorCanvas.DeleteSelection();
            _isModified = true;
            RefreshCanvas();
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            var selBitmap = EditorCanvas.GetSelectionBitmap();
            if (selBitmap is null) return;

            EditorCanvas.ClipboardBitmap?.Dispose();
            EditorCanvas.ClipboardBitmap = selBitmap;
        }

        private void OnPasteClick(object? sender, RoutedEventArgs e)
        {
            if (EditorCanvas.ClipboardBitmap is null) return;
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;

            _historyService.PushState(active.Bitmap);
            EditorCanvas.PasteBitmap(EditorCanvas.ClipboardBitmap);
            _isModified = true;
            RefreshCanvas();
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

        private async void OnImageSizeClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;

            var dialog = new ResizeDialog(active.Bitmap.Width, active.Bitmap.Height);
            await dialog.ShowDialog(this);
            if (!dialog.DialogResult) return;

            _historyService.PushState(active.Bitmap);
            EditorCanvas.DeselectAll();
            var result = _filterService.Resize(active.Bitmap, dialog.NewWidth, dialog.NewHeight);
            active.SetBitmap(result);
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        private async void OnCanvasSizeClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            int w = active?.Bitmap?.Width ?? 800;
            int h = active?.Bitmap?.Height ?? 600;

            var dialog = new ResizeDialog(w, h);
            await dialog.ShowDialog(this);
            if (!dialog.DialogResult) return;

            if (active?.Bitmap is not null)
                _historyService.PushState(active.Bitmap);

            EditorCanvas.DeselectAll();
            var newBitmap = new SkiaSharp.SKBitmap(dialog.NewWidth, dialog.NewHeight,
                SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using (var canvas = new SkiaSharp.SKCanvas(newBitmap))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                if (active?.Bitmap is not null)
                    canvas.DrawBitmap(active.Bitmap, new SkiaSharp.SKPoint(0, 0),
                        new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Nearest, SkiaSharp.SKMipmapMode.None));
            }

            active?.SetBitmap(newBitmap);
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnRotateRightClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;
            _historyService.PushState(active.Bitmap);
            EditorCanvas.DeselectAll();
            EditorCanvas.RotateRight();
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnRotateLeftClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;
            _historyService.PushState(active.Bitmap);
            EditorCanvas.DeselectAll();
            EditorCanvas.RotateLeft();
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;
            _historyService.PushState(active.Bitmap);
            EditorCanvas.DeselectAll();
            EditorCanvas.FlipHorizontalImage();
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        private void OnFlipVerticalClick(object? sender, RoutedEventArgs e)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;
            _historyService.PushState(active.Bitmap);
            EditorCanvas.DeselectAll();
            EditorCanvas.FlipVerticalImage();
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
        }

        #endregion

        #region Layer menu

        private void OnNewLayerClick(object? sender, RoutedEventArgs e)
        {
            int w = 800, h = 600;
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is not null) { w = active.Bitmap.Width; h = active.Bitmap.Height; }
            _layerService.AddEmpty(w, h);
            Layers.Refresh();
            RefreshCanvas();
            UpdateMenuStates();
        }

        private void OnDuplicateLayerClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService.Count == 0) return;
            _layerService.DuplicateLayer(_layerService.ActiveIndex);
            Layers.Refresh();
            RefreshCanvas();
            UpdateMenuStates();
        }

        private void OnDeleteLayerClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService.Count == 0) return;
            _layerService.Remove(_layerService.ActiveIndex);
            Layers.Refresh();
            RefreshCanvas();
            UpdateMenuStates();
        }

        private void OnFlattenClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService.Count <= 1) return;
            _layerService.Flatten();
            Layers.Refresh();
            RefreshCanvas();
            _isModified = true;
            UpdateTitle();
            UpdateMenuStates();
        }

        #endregion

        #region Select menu

        private void OnSelectAllClick(object? sender, RoutedEventArgs e) => EditorCanvas.SelectAll();
        private void OnDeselectClick(object? sender, RoutedEventArgs e) => EditorCanvas.DeselectAll();
        private void OnInvertSelectionClick(object? sender, RoutedEventArgs e) => EditorCanvas.InvertSelection();

        #endregion

        #region Filters menu

        private async void OnBrightnessContrastClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var active = _layerService.ActiveLayer;
                if (active?.Bitmap is null) return;

                var dialog = new FilterDialog(active.Bitmap, _filterService);
                await dialog.ShowDialog(this);
                if (!dialog.DialogResult) return;

                _historyService.PushState(active.Bitmap);
                var brightBitmap = _filterService.AdjustBrightness(active.Bitmap, dialog.Brightness);
                var resultBitmap = _filterService.AdjustContrast(brightBitmap, dialog.Contrast);
                brightBitmap.Dispose();
                active.SetBitmap(resultBitmap);
                _isModified = true;
                RefreshCanvas();
                UpdateTitle();
                UpdateMenuStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Filter error: {ex}");
                StatusText.Text = $"Ошибка при применении фильтра: {ex.Message}";
            }
        }

        private async void OnErodeClick(object? sender, RoutedEventArgs e) => await ApplyMorphologyAsync(MorphologyType.Erode, "Эрозия");
        private async void OnDilateClick(object? sender, RoutedEventArgs e) => await ApplyMorphologyAsync(MorphologyType.Dilate, "Дилатация");
        private async void OnOpenMorphologyClick(object? sender, RoutedEventArgs e) => await ApplyMorphologyAsync(MorphologyType.Open, "Открытие");
        private async void OnCloseMorphologyClick(object? sender, RoutedEventArgs e) => await ApplyMorphologyAsync(MorphologyType.Close, "Закрытие");

        private async Task ApplyMorphologyAsync(MorphologyType type, string title)
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is null) return;

            var dialog = new FilterPreviewDialog(active.Bitmap,
                bmp => _filterService.ApplyMorphology(bmp, type), title);
            await dialog.ShowDialog(this);
            if (!dialog.DialogResult) return;

            _historyService.PushState(active.Bitmap);
            var newBitmap = _filterService.ApplyMorphology(active.Bitmap, type);
            active.SetBitmap(newBitmap);
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
            UpdateMenuStates();
        }

        private async void OnGaussianBlurClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var active = _layerService.ActiveLayer;
                if (active?.Bitmap is null) return;

                var dialog = new GaussianBlurDialog(active.Bitmap, _filterService);
                await dialog.ShowDialog(this);
                if (!dialog.DialogResult) return;

                _historyService.PushState(active.Bitmap);
                var result = _filterService.ApplyGaussianBlur(active.Bitmap, dialog.Radius);
                active.SetBitmap(result);
                _isModified = true;
                RefreshCanvas();
                UpdateTitle();
                UpdateMenuStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GaussianBlur error: {ex}");
                StatusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        private async void OnSharpenClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var active = _layerService.ActiveLayer;
                if (active?.Bitmap is null) return;

                var dialog = new FilterPreviewDialog(active.Bitmap,
                    bmp => _filterService.ApplySharpen(bmp), "Резкость");
                await dialog.ShowDialog(this);
                if (!dialog.DialogResult) return;

                _historyService.PushState(active.Bitmap);
                var result = _filterService.ApplySharpen(active.Bitmap);
                active.SetBitmap(result);
                _isModified = true;
                RefreshCanvas();
                UpdateTitle();
                UpdateMenuStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sharpen error: {ex}");
                StatusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        #endregion

        #region Help menu

        private async void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "О программе",
                Width = 380,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = Avalonia.Media.Brushes.Black
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "Semantic Raster Editor",
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Версия 1.0",
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B5B5B5")),
                FontSize = 12
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Растровый редактор с интеллектуальным анализом изображений.\n" +
                       "Технологии: .NET 9, Avalonia, SkiaSharp, OpenCV",
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B5B5B5")),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var btn = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 28,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2A86E8")),
                Foreground = Avalonia.Media.Brushes.White
            };
            btn.Click += (_, _) => dialog.Close();
            panel.Children.Add(btn);

            dialog.Content = panel;
            await dialog.ShowDialog(this);
        }

        #endregion

        #region Tool and canvas events

        private void OnToolSelected(string tool)
        {
            EditorCanvas.CurrentTool = tool;
            EditorCanvas.SetToolCursor();

            if (ToolOptions is not null)
            {
                EditorCanvas.BrushSize = ToolOptions.BrushSize;
                EditorCanvas.BrushOpacity = (float)(ToolOptions.OpacityValue / 100.0);
                EditorCanvas.CurrentBlendMode = ToolOptions.BlendMode;
            }
        }

        private void OnLayerChanged()
        {
            RefreshCanvas();
            UpdateMenuStates();
        }

        private void OnChannelVisibilityChanged()
        {
            RefreshCanvas();
        }

        private void OnCanvasImageModified()
        {
            _isModified = true;
            RefreshCanvas();
            UpdateTitle();
            UpdateMenuStates();
        }

        private void OnBeforeImageModified()
        {
            var active = _layerService.ActiveLayer;
            if (active?.Bitmap is not null)
                _historyService.PushState(active.Bitmap);
        }

        private void OnZoomChanged(float zoom)
        {
            UpdateStatusBar();
            UpdateDocumentTab();
            UpdateRulers();
        }

        private void OnCursorPositionChanged(float x, float y)
        {
            if (CursorPosText is not null) CursorPosText.Text = $"X: {(int)x}  Y: {(int)y}";
            if (RulerH is not null) RulerH.CursorPosition = x;
            if (RulerV is not null) RulerV.CursorPosition = y;
        }

        #endregion

        #region Keyboard shortcuts

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (ctrl && shift && e.Key == Key.S) { OnSaveAsClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.S) { OnSaveClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.O) { OnOpenClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.Z) { OnUndoClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.Y) { OnRedoClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.X) { OnCutClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.C) { OnCopyClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.V) { OnPasteClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.A) { OnSelectAllClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.D) { OnDeselectClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == Key.J) { OnFlattenClick(sender, new RoutedEventArgs()); e.Handled = true; }
            else if (!ctrl && !shift)
            {
                if (e.Key == Key.Escape)
                {
                    EditorCanvas.DeselectAll();
                    EditorCanvas.CurrentTool = "Hand";
                    ToolPalette.SelectTool("Hand");
                    EditorCanvas.SetToolCursor();
                    e.Handled = true;
                }
                else if (e.Key == Key.Delete)
                {
                    EditorCanvas.DeleteSelection();
                    e.Handled = true;
                }
                else
                {
                    var tool = e.Key switch
                    {
                        Key.V => "Move", Key.M => "Marquee", Key.L => "Lasso",
                        Key.B => "Brush", Key.E => "Eraser",
                        Key.T => "Text", Key.I => "Eyedropper", Key.C => "Crop",
                        Key.H => "Hand", Key.Z => "Zoom", _ => null
                    };
                    if (tool != null) { ToolPalette.SelectTool(tool); e.Handled = true; }
                }
            }
        }

        #endregion

        #region Helpers

        private void RefreshCanvas()
        {
            if (_layerService.Count == 0)
            {
                EditorCanvas.SetImage(null);
                Layers.Channels?.SetImage(null);
                return;
            }

            var active = _layerService.ActiveLayer;

            if (_layerService.Count == 1 && active?.Bitmap is not null &&
                !active.HasMask && active.Opacity >= 1f)
            {
                if (Layers.Channels is not null)
                {
                    var vis = Layers.Channels.ChannelVisibility;
                    if (vis[0] && vis[1] && vis[2] && vis[3] && vis[4])
                    {
                        EditorCanvas.SetImage(active.Bitmap);
                        Layers.Channels.SetImage(active.Bitmap);
                        UpdateStatusBar();
                        UpdateDocumentTab();
                        return;
                    }
                }
                else
                {
                    EditorCanvas.SetImage(active.Bitmap);
                    Layers.Channels?.SetImage(active.Bitmap);
                    UpdateStatusBar();
                    UpdateDocumentTab();
                    return;
                }
            }

            using var composite = _layerService.Composite();

            if (Layers.Channels is not null)
            {
                var visibility = Layers.Channels.ChannelVisibility;
                bool hasHidden = !visibility[0] || !visibility[1] || !visibility[2] || !visibility[3] || !visibility[4];
                if (hasHidden)
                {
                    using var filtered = CanvasControl.ApplyChannelFilter(composite, visibility);
                    EditorCanvas.SetImage(filtered);
                }
                else
                {
                    EditorCanvas.SetImage(composite);
                }
            }
            else
            {
                EditorCanvas.SetImage(composite);
            }

            Layers.Channels?.SetImage(_layerService.ActiveLayer?.Bitmap);
            UpdateStatusBar();
            UpdateDocumentTab();
        }

        private void UpdateStatusBar()
        {
            var zoomPercent = (int)(EditorCanvas.Zoom * 100);
            if (ZoomText is not null) ZoomText.Text = $"{zoomPercent}%";
            if (_layerService.Count > 0 && _layerService.ActiveLayer?.Bitmap is not null)
            {
                var bmp = _layerService.ActiveLayer.Bitmap;
                if (SizeText is not null) SizeText.Text = $"{bmp.Width} x {bmp.Height} px";
                if (PpiText is not null) PpiText.Text = "RGB/8";
            }
            else
            {
                if (SizeText is not null) SizeText.Text = string.Empty;
                if (PpiText is not null) PpiText.Text = string.Empty;
            }
        }

        private void UpdateDocumentTab()
        {
            var fileName = _layerService.ActiveLayer?.Name ?? "Без имени";
            DocTab?.UpdateInfo(fileName, EditorCanvas.Zoom);
        }

        private void UpdateRulers()
        {
            if (RulerH is not null) { RulerH.Zoom = EditorCanvas.Zoom; RulerH.Offset = EditorCanvas.PanOffset.X; }
            if (RulerV is not null) { RulerV.Zoom = EditorCanvas.Zoom; RulerV.Offset = EditorCanvas.PanOffset.Y; }
        }

        private void UpdateTitle()
        {
            var fileName = _layerService.ActiveLayer?.Name ?? "Без имени";
            Title = $"RasterEditor — {fileName}{(_isModified ? " *" : "")}";
        }

        private void UpdateMenuStates()
        {
            var hasImage = _layerService.Count > 0;
            var hasActive = hasImage && _layerService.ActiveLayer?.Bitmap is not null;

            SetMenuItemEnabled("MenuEdit", 0, _historyService.CanUndo);
            SetMenuItemEnabled("MenuEdit", 1, _historyService.CanRedo);
            SetMenuItemEnabled("MenuEdit", 3, hasActive && EditorCanvas.HasSelection);
            SetMenuItemEnabled("MenuEdit", 4, hasActive && EditorCanvas.HasSelection);
            SetMenuItemEnabled("MenuEdit", 5, hasActive && EditorCanvas.ClipboardBitmap is not null);

            for (int i = 0; i < 6; i++) SetMenuItemEnabled("MenuImage", i, hasActive);
            SetMenuItemEnabled("MenuLayer", 0, hasActive);
            SetMenuItemEnabled("MenuLayer", 1, hasActive);
            SetMenuItemEnabled("MenuLayer", 2, hasActive && _layerService.Count > 1);
            SetMenuItemEnabled("MenuLayer", 3, hasActive && _layerService.Count > 1);
            for (int i = 0; i < 3; i++) SetMenuItemEnabled("MenuSelect", i, hasActive);
            for (int i = 0; i < 10; i++) SetMenuItemEnabled("MenuFilters", i, hasActive);

            ToolOptions?.SetUndoRedoState(_historyService.CanUndo, _historyService.CanRedo);
        }

        private void SetMenuItemEnabled(string menuName, int itemIndex, bool enabled)
        {
            if (this.FindControl<MenuItem>(menuName) is not { } menu) return;
            if (itemIndex < 0 || itemIndex >= menu.Items.Count) return;
            if (menu.Items[itemIndex] is MenuItem item) item.IsEnabled = enabled;
        }

        #endregion
    }
}
