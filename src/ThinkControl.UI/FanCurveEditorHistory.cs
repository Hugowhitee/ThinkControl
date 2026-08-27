using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Cooling;

namespace ThinkControl.UI;

internal static class FanCurveEditorHistory
{
    private const int MaxHistory = 60;
    private static readonly ConditionalWeakTable<FanCurveEditorWindow, Session> Sessions = new();

    internal static void Attach(FanCurveEditorWindow window)
    {
        if (Sessions.TryGetValue(window, out _))
            return;

        FanCurveGraph? graph = FindDescendant<FanCurveGraph>(window);
        if (graph is null)
            return;

        var session = new Session(window, graph);
        Sessions.Add(window, session);
        session.Attach();
    }

    private sealed class Session
    {
        private readonly FanCurveEditorWindow _window;
        private readonly FanCurveGraph _graph;
        private readonly Stack<CurveState> _undo = new();
        private readonly Stack<CurveState> _redo = new();
        private Button? _undoButton;
        private Button? _redoButton;
        private CurveState _last;
        private CurveState? _pointerStart;
        private bool _pointerEditing;
        private bool _pointerChanged;
        private bool _restoring;

        internal Session(FanCurveEditorWindow window, FanCurveGraph graph)
        {
            _window = window;
            _graph = graph;
            _last = Capture();
        }

        internal void Attach()
        {
            InstallButtons();
            _graph.CurveChanged += Graph_CurveChanged;
            _graph.PreviewMouseLeftButtonDown += Graph_PreviewMouseLeftButtonDown;
            _graph.PreviewMouseLeftButtonUp += Graph_PreviewMouseLeftButtonUp;
            _window.PreviewKeyDown += Window_PreviewKeyDown;
            _window.Closed += Window_Closed;
            UpdateButtons();
        }

        private void InstallButtons()
        {
            Button? addPoint = FindDescendants<Button>(_window)
                .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "+ Point", StringComparison.Ordinal));
            if (addPoint?.Parent is not StackPanel tools)
                return;

            _undoButton = CreateHistoryButton("Undo", "Undo curve edit · Ctrl+Z");
            _redoButton = CreateHistoryButton("Redo", "Redo curve edit · Ctrl+Y / Ctrl+Shift+Z");
            _undoButton.Click += (_, _) => Undo();
            _redoButton.Click += (_, _) => Redo();
            _redoButton.Margin = new Thickness(2, 0, 6, 0);
            _undoButton.Margin = new Thickness(0, 0, 2, 0);

            int index = Math.Max(0, tools.Children.IndexOf(addPoint));
            tools.Children.Insert(index, _undoButton);
            tools.Children.Insert(index + 1, _redoButton);
        }

        private Button CreateHistoryButton(string text, string tooltip)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(7, 4, 7, 4),
                FontSize = TypographyScale.Caption,
                ToolTip = tooltip,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Style = _window.TryFindResource("TcInlineButton") as Style
            };
            return button;
        }

        private void Graph_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_restoring)
                return;
            _pointerEditing = true;
            _pointerChanged = false;
            _pointerStart = Capture();
        }

        private void Graph_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_pointerEditing)
                return;

            CurveState current = Capture();
            if (_pointerChanged && _pointerStart is CurveState start && !Equivalent(start, current))
            {
                PushUndo(start);
                _redo.Clear();
            }

            _last = current;
            _pointerStart = null;
            _pointerEditing = false;
            _pointerChanged = false;
            UpdateButtons();
        }

        private void Graph_CurveChanged(object? sender, EventArgs e)
        {
            if (_restoring)
                return;

            CurveState current = Capture();
            if (_pointerEditing)
            {
                _pointerChanged |= _pointerStart is CurveState start && !Equivalent(start, current);
                _last = current;
                return;
            }

            if (!Equivalent(_last, current))
            {
                PushUndo(_last);
                _redo.Clear();
                _last = current;
                UpdateButtons();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.FocusedElement is TextBox)
                return;

            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (e.Key == Key.Z && !shift)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y || (e.Key == Key.Z && shift))
            {
                Redo();
                e.Handled = true;
            }
        }

        private void Undo()
        {
            if (_undo.Count == 0)
                return;
            CurveState current = Capture();
            CurveState previous = _undo.Pop();
            PushRedo(current);
            Restore(previous);
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;
            CurveState current = Capture();
            CurveState next = _redo.Pop();
            PushUndo(current);
            Restore(next);
        }

        private void Restore(CurveState state)
        {
            _restoring = true;
            try
            {
                _graph.SetCurve(state.Points);
                _graph.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, Math.Max(0, _graph.PointCount - 1));
                _last = Capture();
            }
            finally
            {
                _restoring = false;
            }
            UpdateButtons();
        }

        private void PushUndo(CurveState state)
        {
            if (_undo.Count > 0 && Equivalent(_undo.Peek(), state))
                return;
            _undo.Push(state.Clone());
            TrimStack(_undo);
        }

        private void PushRedo(CurveState state)
        {
            if (_redo.Count > 0 && Equivalent(_redo.Peek(), state))
                return;
            _redo.Push(state.Clone());
            TrimStack(_redo);
        }

        private static void TrimStack(Stack<CurveState> stack)
        {
            if (stack.Count <= MaxHistory)
                return;
            CurveState[] newestFirst = stack.Take(MaxHistory).ToArray();
            stack.Clear();
            for (int i = newestFirst.Length - 1; i >= 0; i--)
                stack.Push(newestFirst[i]);
        }

        private CurveState Capture() => new(_graph.GetCurve(), _graph.SelectedIndex);

        private static bool Equivalent(CurveState a, CurveState b) =>
            a.Points.SequenceEqual(b.Points);

        private void UpdateButtons()
        {
            if (_undoButton is not null)
                _undoButton.IsEnabled = _undo.Count > 0;
            if (_redoButton is not null)
                _redoButton.IsEnabled = _redo.Count > 0;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _graph.CurveChanged -= Graph_CurveChanged;
            _graph.PreviewMouseLeftButtonDown -= Graph_PreviewMouseLeftButtonDown;
            _graph.PreviewMouseLeftButtonUp -= Graph_PreviewMouseLeftButtonUp;
            _window.PreviewKeyDown -= Window_PreviewKeyDown;
            _window.Closed -= Window_Closed;
        }
    }

    private sealed record CurveState(FanCurvePoint[] Points, int SelectedIndex)
    {
        internal CurveState Clone() => new(Points.Select(point => point with { }).ToArray(), SelectedIndex);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            T? nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (T nested in FindDescendants<T>(child))
                yield return nested;
        }
    }
}
