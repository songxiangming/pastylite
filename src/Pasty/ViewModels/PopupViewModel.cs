using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Pasty.Data;
using Pasty.Models;
using Pasty.Services;

namespace Pasty.ViewModels;

public class PopupViewModel : INotifyPropertyChanged
{
    private readonly ClipboardStore _store;
    private readonly PasteService _pasteService;
    private readonly FuzzySearchService _searchService;
    private readonly DispatcherTimer _debounceTimer;

    private string _searchText = "";
    private int _selectedIndex;
    private List<ClipboardItemViewModel> _allItems = [];

    public ObservableCollection<ClipboardItemViewModel> FilteredItems { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            OnPropertyChanged();
        }
    }

    public PopupViewModel(ClipboardStore store, PasteService pasteService)
    {
        _store = store;
        _pasteService = pasteService;
        _searchService = new FuzzySearchService();

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            ApplyFilter();
        };
    }

    public async Task LoadItemsAsync()
    {
        var items = await _store.GetRecentAsync(200);
        _allItems = items.Select(i => new ClipboardItemViewModel(i)).ToList();
        SearchText = "";
        ApplyFilter();
    }

    public void AddNewItem(ClipboardItem item)
    {
        // Remove existing duplicate from the in-memory list (by content hash)
        _allItems.RemoveAll(vm => vm.Id == item.Id);
        // Insert at top
        _allItems.Insert(0, new ClipboardItemViewModel(item));
        // Keep max 200
        if (_allItems.Count > 200)
            _allItems.RemoveAt(_allItems.Count - 1);
        ApplyFilter();
    }

    public void BumpItem(long id)
    {
        var existing = _allItems.FirstOrDefault(vm => vm.Id == id);
        if (existing != null)
        {
            _allItems.Remove(existing);
            _allItems.Insert(0, existing);
            ApplyFilter();
        }
    }

    public void UpdateOcrText(long id, string ocrText)
    {
        var existing = _allItems.FirstOrDefault(vm => vm.Id == id);
        if (existing != null)
        {
            existing.SetOcrText(ocrText);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var filtered = _searchService.Filter(_allItems, _searchText);
        FilteredItems.Clear();
        foreach (var item in filtered)
            FilteredItems.Add(item);
        SelectedIndex = FilteredItems.Count > 0 ? 0 : -1;
    }

    public void MoveSelection(int delta)
    {
        if (FilteredItems.Count == 0) return;
        var newIndex = SelectedIndex + delta;
        if (newIndex < 0) newIndex = 0;
        if (newIndex >= FilteredItems.Count) newIndex = FilteredItems.Count - 1;
        SelectedIndex = newIndex;
    }

    public async Task PasteSelectedAsync(bool plainTextOnly)
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count) return;
        var selected = FilteredItems[SelectedIndex];
        await _pasteService.PasteItemAsync(selected.Id, plainTextOnly);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
