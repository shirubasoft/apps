namespace QuietNight.Ui;

/// <summary>Tiles that switch on and off independently; at least one always stays on.</summary>
public sealed class ToggleRow : Grid
{
    private readonly List<Tile> tiles = [];
    private readonly bool[] on;

    public ToggleRow(IReadOnlyList<string> labels, IReadOnlyList<bool> on, IReadOnlyList<string>? icons = null, (string On, string Off)? states = null)
    {
        this.on = [.. on];
        ColumnSpacing = 10;
        RowSpacing = 10;
        // With large text the names no longer fit side by side, so the tiles stack with the icon beside each name.
        var across = Theme.LargeText ? 1 : labels.Count;
        for (var c = 0; c < across; c++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var tile = new Tile(icons?[i], labels[i], stacked: across > 1, states ?? ("on", "off")) { AutomationId = $"toggle-{labels[i].ToLowerInvariant()}" };
            tile.Tapped += () => Toggle(index);
            tiles.Add(tile);
            if (i % across == 0) RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            this.Add(tile, i % across, i / across);
        }
        Paint();
    }

    /// <summary>Raised with the index of the tile and whether it is now on.</summary>
    public event Action<int, bool>? Changed;

    /// <summary>Raised when the user tries to switch off the last tile that is on.</summary>
    public event Action? KeptLast;

    public void SetEnabled(bool enabled)
    {
        foreach (var tile in tiles)
            tile.SetEnabled(enabled);
    }

    private void Toggle(int index)
    {
        if (on[index] && on.Count(o => o) == 1)
        {
            KeptLast?.Invoke();
            return;
        }
        on[index] = !on[index];
        Paint();
        Changed?.Invoke(index, on[index]);
    }

    private void Paint()
    {
        for (var i = 0; i < tiles.Count; i++)
            tiles[i].Selected = on[i];
    }
}
