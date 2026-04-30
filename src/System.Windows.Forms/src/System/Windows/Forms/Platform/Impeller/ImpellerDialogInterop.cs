// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms.Platform;

/// <summary>
///  Managed WinFormsX dialog interop for common dialogs.
/// </summary>
internal sealed class ImpellerDialogInterop : IDialogInterop
{
    public string? ShowOpenFileDialog(nint owner, string? title, string? filter, string? initialDir, string? fileName)
    {
        using FilePickerDialog dialog = new(owner, saveMode: false, title, filter, initialDir, fileName);
        return dialog.ShowDialog(new WindowWrapper(owner)) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public string? ShowSaveFileDialog(nint owner, string? title, string? filter, string? initialDir, string? fileName)
    {
        using FilePickerDialog dialog = new(owner, saveMode: true, title, filter, initialDir, fileName);
        return dialog.ShowDialog(new WindowWrapper(owner)) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public bool ShowColorDialog(nint owner, ref Color color)
    {
        using ColorPickerDialog dialog = new(owner, color);
        if (dialog.ShowDialog(new WindowWrapper(owner)) != DialogResult.OK)
        {
            return false;
        }

        color = dialog.SelectedColor;
        return true;
    }

    public bool ShowFontDialog(nint owner, ref Font font)
    {
        using FontPickerDialog dialog = new(owner, font);
        if (dialog.ShowDialog(new WindowWrapper(owner)) != DialogResult.OK)
        {
            return false;
        }

        font = dialog.SelectedFont;
        return true;
    }

    public string? ShowFolderBrowserDialog(nint owner, string? description, string? initialDirectory, string? selectedPath)
    {
        using FolderPickerDialog dialog = new(owner, description, initialDirectory, selectedPath);
        return dialog.ShowDialog(new WindowWrapper(owner)) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void NotifyOwnerIdle(nint owner, nint dialogHandle)
    {
        if (owner == 0 || dialogHandle == 0)
        {
            return;
        }

        PInvoke.PostMessage(
            (HWND)owner,
            PInvoke.WM_ENTERIDLE,
            (WPARAM)0,
            (LPARAM)dialogHandle);
    }

    private sealed class WindowWrapper(nint handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    private abstract class ManagedDialogForm : Form
    {
        private readonly nint _owner;

        protected ManagedDialogForm(nint owner)
        {
            _owner = owner;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
        }

        protected sealed override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke(() => NotifyOwnerIdle(_owner, Handle));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.MsgInternal == PInvoke.WM_COMMAND)
            {
                switch ((int)m.WParamInternal.LOWORD)
                {
                    case (int)MESSAGEBOX_RESULT.IDOK:
                        AcceptDialog();
                        return;
                    case (int)MESSAGEBOX_RESULT.IDCANCEL:
                        DialogResult = DialogResult.Cancel;
                        Close();
                        return;
                }
            }

            base.WndProc(ref m);
        }

        internal abstract void AcceptDialog();
    }

    private sealed class FilePickerDialog : ManagedDialogForm
    {
        private readonly bool _saveMode;
        private readonly TextBox _pathBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        private readonly ListBox _items = new() { Dock = DockStyle.Fill };
        private string _currentDirectory;

        public FilePickerDialog(nint owner, bool saveMode, string? title, string? filter, string? initialDir, string? fileName)
            : base(owner)
        {
            _saveMode = saveMode;
            Text = string.IsNullOrWhiteSpace(title) ? (saveMode ? "Save File" : "Open File") : title;
            ClientSize = new Size(720, 460);
            MinimumSize = new Size(520, 320);

            _currentDirectory = ResolveInitialDirectory(initialDir, fileName);
            _pathBox.Text = ResolveInitialPath(_currentDirectory, fileName);

            Controls.Add(CreateContent(filter));
            LoadDirectory(_currentDirectory);
        }

        public string SelectedPath { get; private set; } = string.Empty;

        internal override void AcceptDialog()
        {
            string path = ResolvePath(_pathBox.Text);
            if (string.IsNullOrWhiteSpace(path))
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            SelectedPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }

        private TableLayoutPanel CreateContent(string? filter)
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label current = new()
            {
                AutoSize = true,
                Text = _currentDirectory,
                Padding = new Padding(0, 0, 0, 6)
            };
            root.Controls.Add(current, 0, 0);

            TableLayoutPanel pathRow = new()
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                AutoSize = true
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.Controls.Add(new Label { AutoSize = true, Text = _saveMode ? "File name:" : "File:" }, 0, 0);
            pathRow.Controls.Add(_pathBox, 1, 0);

            Button up = new() { Text = "Up", AutoSize = true };
            up.Click += (sender, e) => NavigateUp(current);
            pathRow.Controls.Add(up, 2, 0);
            root.Controls.Add(pathRow, 0, 1);

            _items.DisplayMember = nameof(FileSystemEntry.DisplayName);
            _items.DoubleClick += (sender, e) => ActivateSelection(current);
            root.Controls.Add(_items, 0, 2);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            Button ok = new() { Text = _saveMode ? "Save" : "Open", AutoSize = true, DialogResult = DialogResult.None };
            ok.Click += (sender, e) => AcceptDialog();
            Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            if (!string.IsNullOrEmpty(filter))
            {
                buttons.Controls.Add(new Label { Text = filter, AutoSize = true, Padding = new Padding(0, 6, 20, 0) });
            }

            AcceptButton = ok;
            CancelButton = cancel;
            root.Controls.Add(buttons, 0, 3);
            return root;
        }

        private void ActivateSelection(Label currentLabel)
        {
            if (_items.SelectedItem is not FileSystemEntry entry)
            {
                return;
            }

            if (entry.IsDirectory)
            {
                _currentDirectory = entry.FullPath;
                currentLabel.Text = _currentDirectory;
                LoadDirectory(_currentDirectory);
                return;
            }

            _pathBox.Text = entry.FullPath;
            if (!_saveMode)
            {
                AcceptDialog();
            }
        }

        private void NavigateUp(Label currentLabel)
        {
            string? parent = Directory.GetParent(_currentDirectory)?.FullName;
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            _currentDirectory = parent;
            currentLabel.Text = _currentDirectory;
            LoadDirectory(_currentDirectory);
        }

        private void LoadDirectory(string directory)
        {
            _items.Items.Clear();

            try
            {
                foreach (string childDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    _items.Items.Add(new FileSystemEntry(childDirectory, isDirectory: true));
                }

                foreach (string file in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    _items.Items.Add(new FileSystemEntry(file, isDirectory: false));
                }
            }
            catch
            {
                _items.Items.Add(new FileSystemEntry(directory, isDirectory: true));
            }
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(_currentDirectory, path));
        }

        private static string ResolveInitialDirectory(string? initialDir, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir))
            {
                return Path.GetFullPath(initialDir);
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string? parent = Path.GetDirectoryName(fileName);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return Path.GetFullPath(parent);
                }
            }

            return Environment.CurrentDirectory;
        }

        private static string ResolveInitialPath(string currentDirectory, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return Path.IsPathFullyQualified(fileName) ? fileName : Path.Combine(currentDirectory, fileName);
        }
    }

    private sealed class FolderPickerDialog : ManagedDialogForm
    {
        private readonly TextBox _pathBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        private readonly ListBox _items = new() { Dock = DockStyle.Fill };
        private string _currentDirectory;

        public FolderPickerDialog(nint owner, string? description, string? initialDirectory, string? selectedPath)
            : base(owner)
        {
            Text = string.IsNullOrWhiteSpace(description) ? "Select Folder" : description;
            ClientSize = new Size(680, 430);
            MinimumSize = new Size(500, 300);

            _currentDirectory = ResolveInitialDirectory(initialDirectory, selectedPath);
            _pathBox.Text = !string.IsNullOrWhiteSpace(selectedPath) ? selectedPath : _currentDirectory;
            Controls.Add(CreateContent());
            LoadDirectory(_currentDirectory);
        }

        public string SelectedPath { get; private set; } = string.Empty;

        internal override void AcceptDialog()
        {
            string path = Path.GetFullPath(string.IsNullOrWhiteSpace(_pathBox.Text) ? _currentDirectory : _pathBox.Text);
            SelectedPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }

        private TableLayoutPanel CreateContent()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TableLayoutPanel pathRow = new()
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                AutoSize = true
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.Controls.Add(new Label { AutoSize = true, Text = "Folder:" }, 0, 0);
            pathRow.Controls.Add(_pathBox, 1, 0);

            Button up = new() { Text = "Up", AutoSize = true };
            up.Click += (sender, e) => NavigateUp();
            pathRow.Controls.Add(up, 2, 0);
            root.Controls.Add(pathRow, 0, 0);

            _items.DisplayMember = nameof(FileSystemEntry.DisplayName);
            _items.DoubleClick += (sender, e) => ActivateSelection();
            root.Controls.Add(_items, 0, 1);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            Button ok = new() { Text = "Select", AutoSize = true };
            ok.Click += (sender, e) => AcceptDialog();
            Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;
            root.Controls.Add(buttons, 0, 2);
            return root;
        }

        private void ActivateSelection()
        {
            if (_items.SelectedItem is not FileSystemEntry entry)
            {
                return;
            }

            _currentDirectory = entry.FullPath;
            _pathBox.Text = _currentDirectory;
            LoadDirectory(_currentDirectory);
        }

        private void NavigateUp()
        {
            string? parent = Directory.GetParent(_currentDirectory)?.FullName;
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            _currentDirectory = parent;
            _pathBox.Text = _currentDirectory;
            LoadDirectory(_currentDirectory);
        }

        private void LoadDirectory(string directory)
        {
            _items.Items.Clear();
            try
            {
                foreach (string childDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    _items.Items.Add(new FileSystemEntry(childDirectory, isDirectory: true));
                }
            }
            catch
            {
                _items.Items.Add(new FileSystemEntry(directory, isDirectory: true));
            }
        }

        private static string ResolveInitialDirectory(string? initialDir, string? selectedPath)
        {
            if (!string.IsNullOrWhiteSpace(selectedPath) && Directory.Exists(selectedPath))
            {
                return Path.GetFullPath(selectedPath);
            }

            if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir))
            {
                return Path.GetFullPath(initialDir);
            }

            return Environment.CurrentDirectory;
        }
    }

    private sealed class ColorPickerDialog : ManagedDialogForm
    {
        private readonly Color[] _colors =
        [
            Color.Black, Color.DimGray, Color.Gray, Color.White,
            Color.Red, Color.Orange, Color.Gold, Color.Yellow,
            Color.Green, Color.Teal, Color.Cyan, Color.Blue,
            Color.Navy, Color.Purple, Color.Magenta, Color.Brown
        ];

        private readonly ListBox _items = new() { Dock = DockStyle.Fill };

        public ColorPickerDialog(nint owner, Color color)
            : base(owner)
        {
            SelectedColor = color.IsEmpty ? Color.Black : color;
            Text = "Select Color";
            ClientSize = new Size(360, 360);
            Controls.Add(CreateContent());
        }

        public Color SelectedColor { get; private set; }

        internal override void AcceptDialog()
        {
            if (_items.SelectedItem is Color color)
            {
                SelectedColor = color;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private TableLayoutPanel CreateContent()
        {
            TableLayoutPanel root = CreateSimpleDialogLayout();
            _items.DrawMode = DrawMode.OwnerDrawFixed;
            _items.ItemHeight = 28;
            _items.DrawItem += DrawColorItem;
            foreach (Color color in _colors)
            {
                int index = _items.Items.Add(color);
                if (color.ToArgb() == SelectedColor.ToArgb())
                {
                    _items.SelectedIndex = index;
                }
            }

            root.Controls.Add(_items, 0, 0);
            root.Controls.Add(CreateOkCancelButtons("OK"), 0, 1);
            return root;
        }

        private static void DrawColorItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ListBox list || e.Index < 0 || list.Items[e.Index] is not Color color)
            {
                return;
            }

            e.DrawBackground();
            Rectangle swatch = new(e.Bounds.Left + 6, e.Bounds.Top + 5, 34, e.Bounds.Height - 10);
            using SolidBrush swatchBrush = new(color);
            e.Graphics.FillRectangle(swatchBrush, swatch);
            e.Graphics.DrawRectangle(SystemPens.ControlDark, swatch);
            TextRenderer.DrawText(e.Graphics, color.Name, e.Font, new Point(e.Bounds.Left + 48, e.Bounds.Top + 5), e.ForeColor);
            e.DrawFocusRectangle();
        }
    }

    private sealed class FontPickerDialog : ManagedDialogForm
    {
        private readonly ListBox _items = new() { Dock = DockStyle.Fill };
        private readonly NumericUpDown _size = new()
        {
            Minimum = 1,
            Maximum = 256,
            DecimalPlaces = 1,
            Increment = 1,
            Width = 80
        };

        public FontPickerDialog(nint owner, Font font)
            : base(owner)
        {
            SelectedFont = font;
            Text = "Select Font";
            ClientSize = new Size(460, 430);
            Controls.Add(CreateContent());
        }

        public Font SelectedFont { get; private set; }

        internal override void AcceptDialog()
        {
            if (_items.SelectedItem is string family)
            {
                SelectedFont = new Font(family, (float)_size.Value, SelectedFont.Style, SelectedFont.Unit);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private TableLayoutPanel CreateContent()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel sizeRow = new() { AutoSize = true, Dock = DockStyle.Top };
            sizeRow.Controls.Add(new Label { Text = "Size:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
            _size.Value = (decimal)Math.Clamp(SelectedFont.SizeInPoints, 1, 256);
            sizeRow.Controls.Add(_size);
            root.Controls.Add(sizeRow, 0, 0);

            foreach (FontFamily family in FontFamily.Families.OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase))
            {
                int index = _items.Items.Add(family.Name);
                if (string.Equals(family.Name, SelectedFont.FontFamily.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _items.SelectedIndex = index;
                }
            }

            if (_items.SelectedIndex < 0 && _items.Items.Count > 0)
            {
                _items.SelectedIndex = 0;
            }

            root.Controls.Add(_items, 0, 1);
            root.Controls.Add(CreateOkCancelButtons("OK"), 0, 2);
            return root;
        }
    }

    private sealed class FileSystemEntry(string fullPath, bool isDirectory)
    {
        public string FullPath { get; } = fullPath;

        public bool IsDirectory { get; } = isDirectory;

        public string DisplayName
        {
            get
            {
                string name = Path.GetFileName(FullPath);
                if (string.IsNullOrEmpty(name))
                {
                    name = FullPath;
                }

                return IsDirectory ? $"[{name}]" : name;
            }
        }
    }

    private static TableLayoutPanel CreateSimpleDialogLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return root;
    }

    private static FlowLayoutPanel CreateOkCancelButtons(string okText)
    {
        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        Button ok = new() { Text = okText, AutoSize = true };
        ok.Click += (sender, e) => ((ManagedDialogForm)((Control)sender!).FindForm()!).AcceptDialog();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        return buttons;
    }
}
