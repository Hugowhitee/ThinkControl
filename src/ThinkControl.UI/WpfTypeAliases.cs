// ThinkControl.UI is a WPF application that also enables WinForms for the tray icon.
// Keep ambiguous unqualified UI primitives on the WPF side; WinForms-only code should
// continue to use System.Windows.Forms or its existing explicit aliases.
global using Application = System.Windows.Application;
global using SystemColors = System.Windows.SystemColors;
global using Cursors = System.Windows.Input.Cursors;
global using TextBox = System.Windows.Controls.TextBox;
global using ContextMenu = System.Windows.Controls.ContextMenu;
global using MenuItem = System.Windows.Controls.MenuItem;
global using FlowDirection = System.Windows.FlowDirection;
