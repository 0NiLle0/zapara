using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Vograph.Helpers;

// Makes a ComboBox searchable: the editable text field acts as a search box,
// filtering dropdown items by substring (case-insensitive).
// Requires IsEditable="True" + TextSearch.TextPath set in XAML so the
// selected item still displays correctly (e.g. Group.Name, MapInfo.Title).
// Code comments in English, no UI strings here.
public static class SearchableComboBox
{
    private static readonly HashSet<ComboBox> _attached = new();
    private static readonly Dictionary<ComboBox, State> _states = new();

    private sealed class State
    {
        public Func<object, string> TextOf = _ => "";
        public bool Suppress;
    }

    public static void Enable(ComboBox combo, Func<object, string> textOf)
    {
        if (combo == null) return;
        if (_states.TryGetValue(combo, out var existing))
        {
            existing.TextOf = textOf;
            return;
        }
        var state = new State { TextOf = textOf };
        _states[combo] = state;

        combo.IsEditable = true;
        combo.IsTextSearchEnabled = false;
        combo.StaysOpenOnEdit = true;

        if (combo.IsLoaded) AttachBox(combo, state);
        else combo.Loaded += (_, _) => AttachBox(combo, state);

        combo.SelectionChanged += (_, _) => ClearFilter(combo);
        combo.DropDownClosed += (_, _) =>
        {
            ClearFilter(combo);
            // restore selected item text after filtering (e.g. user typed w/o picking)
            try
            {
                if (combo.SelectedItem != null)
                {
                    var box = FindBox(combo);
                    if (box != null)
                    {
                        state.Suppress = true;
                        combo.Text = state.TextOf(combo.SelectedItem);
                        state.Suppress = false;
                    }
                }
            }
            catch { }
        };
        combo.DropDownOpened += (_, _) =>
        {
            // focus search field + select all so typing replaces immediately
            try
            {
                var box = FindBox(combo);
                if (box != null)
                    combo.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { box.Focus(); box.SelectAll(); } catch { }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch { }
        };
        combo.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && combo.IsDropDownOpen)
            {
                // pick first visible match on Enter
                try
                {
                    var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
                    var first = view?.Cast<object>().FirstOrDefault();
                    if (first != null && !Equals(combo.SelectedItem, first))
                    {
                        combo.SelectedItem = first;
                        combo.IsDropDownOpen = false;
                        e.Handled = true;
                    }
                }
                catch { }
            }
        };
    }

    private static void AttachBox(ComboBox combo, State state)
    {
        if (_attached.Contains(combo)) return;
        var box = FindBox(combo);
        if (box == null) return;
        _attached.Add(combo);
        box.TextChanged += (_, _) =>
        {
            if (state.Suppress) return;
            // selection sync (selecting an item sets Text) -> clear filter, not a search
            try
            {
                if (combo.SelectedItem != null &&
                    string.Equals(box.Text, state.TextOf(combo.SelectedItem), StringComparison.Ordinal))
                    return;
            }
            catch { }
            ApplyFilter(combo, state, box.Text);
        };
    }

    private static TextBox? FindBox(ComboBox combo)
    {
        try
        {
            combo.ApplyTemplate();
            return combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;
        }
        catch { return null; }
    }

    private static void ApplyFilter(ComboBox combo, State state, string? text)
    {
        try
        {
            var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
            if (view == null) return;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (view.Filter != null) { view.Filter = null; view.Refresh(); }
            }
            else
            {
                string q = text.Trim();
                view.Filter = o =>
                {
                    try { return state.TextOf(o).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch { return true; }
                };
            }
            if (!combo.IsDropDownOpen) combo.IsDropDownOpen = true;
        }
        catch { }
    }

    private static void ClearFilter(ComboBox combo)
    {
        try
        {
            var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
            if (view != null && view.Filter != null)
            {
                view.Filter = null;
                view.Refresh();
            }
        }
        catch { }
    }
}
