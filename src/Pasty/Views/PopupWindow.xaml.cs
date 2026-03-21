using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Pasty.ViewModels;

using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Pasty.Views;

public partial class PopupWindow : Window
{
    private PopupViewModel? _viewModel;
    private bool _isPasting;

    public PopupWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(PopupViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
    }

    public async Task ShowAndLoadAsync()
    {
        _isPasting = false;
        PositionOnCurrentMonitor();
        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();

        if (_viewModel != null)
            await _viewModel.LoadItemsAsync();
    }

    private void PositionOnCurrentMonitor()
    {
        var cursorPos = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursorPos);
        var workArea = screen.WorkingArea;

        // Get DPI scale
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        // If window hasn't been shown yet, use default scale
        if (scaleX == 0) scaleX = 1.0;
        if (scaleY == 0) scaleY = 1.0;

        Left = (workArea.Left / scaleX) + (workArea.Width / scaleX - Width) / 2;
        Top = (workArea.Top / scaleY) + (workArea.Height / scaleY - Height) / 3;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.Escape:
                HidePopup();
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.MoveSelection(-1);
                ScrollSelectedIntoView();
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.MoveSelection(+1);
                ScrollSelectedIntoView();
                e.Handled = true;
                break;

            case Key.Enter:
                var plainText = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                _ = PasteSelectedAsync(plainText);
                e.Handled = true;
                break;
        }
    }

    private async Task PasteSelectedAsync(bool plainText)
    {
        if (_viewModel == null || _isPasting) return;
        _isPasting = true;

        HidePopup();
        await Task.Delay(50); // Let window hide before pasting
        await _viewModel.PasteSelectedAsync(plainText);
    }

    private void ScrollSelectedIntoView()
    {
        if (_viewModel != null && _viewModel.SelectedIndex >= 0
            && _viewModel.SelectedIndex < ItemList.Items.Count)
        {
            ItemList.ScrollIntoView(ItemList.Items[_viewModel.SelectedIndex]);
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_isPasting)
            HidePopup();
    }

    private void HidePopup()
    {
        Hide();
    }
}
