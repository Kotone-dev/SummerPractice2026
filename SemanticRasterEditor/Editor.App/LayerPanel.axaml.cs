using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Editor.Services;

namespace Editor.App
{
    public partial class LayerPanel : UserControl
    {
        private LayerService? _layerService;
        private bool _isRefreshing;

        public event Action? LayerChanged;

        public LayerPanel()
        {
            InitializeComponent();
            BtnAdd.Click += OnAddClick;
            BtnRemove.Click += OnRemoveClick;
            BtnUp.Click += OnUpClick;
            BtnDown.Click += OnDownClick;
            LayerList.SelectionChanged += OnSelectionChanged;
        }

        public void Bind(LayerService service)
        {
            _layerService = service;
            Refresh();
        }

        public void Refresh()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                RefreshInner();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshInner()
        {
            if (_layerService is null)
                return;

            var prevIndex = LayerList.SelectedIndex;
            LayerList.ItemsSource = null;

            var items = new ListBoxItem[_layerService.Count];
            for (int i = 0; i < _layerService.Count; i++)
            {
                var layer = _layerService.Layers[i];
                var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };

                var cb = new CheckBox
                {
                    IsChecked = layer.IsVisible,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 0, 6, 0)
                };
                var layerRef = layer;
                cb.PointerPressed += (_, _) =>
                {
                    layerRef.IsVisible = !layerRef.IsVisible;
                    Refresh();
                    LayerChanged?.Invoke();
                };
                panel.Children.Add(cb);

                var name = layer.Name;
                if (_layerService.ActiveIndex == i)
                    name += " *";

                var tb = new TextBlock
                {
                    Text = name,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
                };
                panel.Children.Add(tb);

                items[i] = new ListBoxItem { Content = panel };
            }

            LayerList.ItemsSource = items;

            if (prevIndex >= 0 && prevIndex < _layerService.Count)
                LayerList.SelectedIndex = prevIndex;
            else if (_layerService.Count > 0)
                LayerList.SelectedIndex = _layerService.ActiveIndex;
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_layerService is null || LayerList.SelectedIndex < 0)
                return;

            _layerService.ActiveIndex = LayerList.SelectedIndex;
            Refresh();
        }

        private void OnAddClick(object? sender, RoutedEventArgs e)
        {
            LayerChanged?.Invoke();
        }

        private void OnRemoveClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null || _layerService.Count <= 1)
                return;

            _layerService.Remove(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }

        private void OnUpClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null)
                return;

            _layerService.MoveUp(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }

        private void OnDownClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null)
                return;

            _layerService.MoveDown(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }
    }
}
