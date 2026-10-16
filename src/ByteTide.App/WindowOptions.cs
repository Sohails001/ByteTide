namespace ByteTide.App;

/// <summary>Colour scheme of the main window's chrome (menus, title bar, graph).</summary>
public enum ThemeMode
{
    Dark = 0,
    Light = 1,
    /// <summary>Follow the Windows light/dark app setting.</summary>
    System = 2
}

/// <summary>
/// Window behavior preferences, persisted as JSON via Storage's settings
/// table so the window reopens the way you left it.
/// </summary>
public sealed class WindowOptions
{
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 400;

    public bool AlwaysOnTop { get; set; }
    public bool SemiTransparent { get; set; }
    public bool ClickThrough { get; set; }

    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
}
