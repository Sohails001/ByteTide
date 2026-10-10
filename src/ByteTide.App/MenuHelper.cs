namespace ByteTide.App;

internal static class MenuHelper
{
    /// <summary>Removes and disposes every item so rebuilt menus don't leak.</summary>
    public static void Clear(ToolStripItemCollection items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            items.RemoveAt(i);
            item.Dispose();
        }
    }
}
