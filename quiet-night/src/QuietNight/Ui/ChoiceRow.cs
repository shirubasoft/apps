namespace QuietNight.Ui;

/// <summary>Tiles where exactly one is chosen, laid out in rows of <c>columns</c>.</summary>
public sealed class ChoiceRow : Grid
{
    private readonly List<Tile> tiles = [];
    private int selected;

    public ChoiceRow(IReadOnlyList<string> labels, int selected, IReadOnlyList<string>? icons = null, int? columns = null, bool stacked = false,
        IReadOnlyList<string>? notes = null)
    {
        this.selected = selected;
        var across = Theme.LargeText ? 1 : columns ?? labels.Count;
        ColumnSpacing = 10;
        RowSpacing = 10;
        for (var c = 0; c < across; c++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var tile = new Tile(icons?[i], labels[i], stacked, ("selected", "not selected"), notes?[i], single: true);
            tile.Tapped += () => Select(index);
            tiles.Add(tile);
            if (i % across == 0) RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            this.Add(tile, i % across, i / across);
        }
        Paint();
    }

    public event Action<int>? Changed;

    public int Selected => selected;

    /// <summary>Shows a choice made elsewhere, without raising <see cref="Changed"/>; -1 shows none chosen.</summary>
    public void Show(int index)
    {
        selected = index;
        Paint();
    }

    public void SetEnabled(bool enabled)
    {
        foreach (var tile in tiles)
            tile.SetEnabled(enabled);
    }

    private void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        Paint();
        Changed?.Invoke(index);
    }

    private void Paint()
    {
        for (var i = 0; i < tiles.Count; i++)
            tiles[i].Selected = i == selected;
    }
}
