// ThinkControl.UI is a WPF application that also enables WinForms for the tray icon.
// Keep ambiguous unqualified UI primitives on the WPF side; WinForms-only code must
// continue to use System.Windows.Forms or an explicit local alias.
global using Application = System.Windows.Application;
global using Binding = System.Windows.Data.Binding;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
global using CheckBox = System.Windows.Controls.CheckBox;
global using Color = System.Windows.Media.Color;
global using ComboBox = System.Windows.Controls.ComboBox;
global using ContextMenu = System.Windows.Controls.ContextMenu;
global using Control = System.Windows.Controls.Control;
global using Cursors = System.Windows.Input.Cursors;
global using FlowDirection = System.Windows.FlowDirection;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using MenuItem = System.Windows.Controls.MenuItem;
global using MessageBox = System.Windows.MessageBox;
global using Orientation = System.Windows.Controls.Orientation;
global using Panel = System.Windows.Controls.Panel;
global using Pen = System.Windows.Media.Pen;
global using RadioButton = System.Windows.Controls.RadioButton;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using SystemColors = System.Windows.SystemColors;
global using TextBox = System.Windows.Controls.TextBox;
global using UserControl = System.Windows.Controls.UserControl;
