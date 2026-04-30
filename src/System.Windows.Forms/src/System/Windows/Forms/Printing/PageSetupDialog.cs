// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using Windows.Win32.UI.Controls.Dialogs;

namespace System.Windows.Forms;

/// <summary>
///  Represents a dialog box that allows users to manipulate page settings,
///  including margins and paper orientation.
/// </summary>
[DefaultProperty(nameof(Document))]
[SRDescription(nameof(SR.DescriptionPageSetupDialog))]
public sealed class PageSetupDialog : CommonDialog
{
    // If PrintDocument is not null, pageSettings == printDocument.PageSettings
    private PrintDocument? _printDocument;
    private PageSettings? _pageSettings;
    private PrinterSettings? _printerSettings;

    private Margins? _minMargins;

    /// <summary>
    ///  Initializes a new instance of the <see cref="PageSetupDialog"/> class.
    /// </summary>
    public PageSetupDialog() => Reset();

    /// <summary>
    ///  Gets or sets a value indicating whether the margins section of the dialog box is enabled.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(true)]
    [SRDescription(nameof(SR.PSDallowMarginsDescr))]
    public bool AllowMargins { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating whether the orientation section of the dialog box (landscape vs. portrait)
    ///  is enabled.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(true)]
    [SRDescription(nameof(SR.PSDallowOrientationDescr))]
    public bool AllowOrientation { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating whether the paper section of the dialog box (paper size and paper source)
    ///  is enabled.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(true)]
    [SRDescription(nameof(SR.PSDallowPaperDescr))]
    public bool AllowPaper { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating whether the Printer button is enabled.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(true)]
    [SRDescription(nameof(SR.PSDallowPrinterDescr))]
    public bool AllowPrinter { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating the <see cref="PrintDocument"/> to get page settings from.
    /// </summary>
    [SRCategory(nameof(SR.CatData))]
    [DefaultValue(null)]
    [SRDescription(nameof(SR.PDdocumentDescr))]
    public PrintDocument? Document
    {
        get => _printDocument;
        set
        {
            _printDocument = value;
            if (_printDocument is not null)
            {
                _pageSettings = _printDocument.DefaultPageSettings;
                _printerSettings = _printDocument.PrinterSettings;
            }
        }
    }

    /// <summary>
    ///  This allows the user to override the current behavior where the Metric is converted to ThousandOfInch even
    ///  for METRIC MEASUREMENTSYSTEM which returns a HUNDREDSOFMILLIMETER value.
    /// </summary>
    [DefaultValue(false)]
    [SRDescription(nameof(SR.PSDenableMetricDescr))]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public bool EnableMetric { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating the minimum margins the user is allowed to select,
    ///  in hundredths of an inch.
    /// </summary>
    [SRCategory(nameof(SR.CatData))]
    [SRDescription(nameof(SR.PSDminMarginsDescr))]
    public Margins? MinMargins
    {
        get => _minMargins;
        set => _minMargins = value ?? new Margins(0, 0, 0, 0);
    }

    /// <summary>
    ///  Gets or sets a value indicating the page settings modified by the dialog box.
    /// </summary>
    [SRCategory(nameof(SR.CatData))]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [SRDescription(nameof(SR.PSDpageSettingsDescr))]
    public PageSettings? PageSettings
    {
        get => _pageSettings;
        set
        {
            _pageSettings = value;
            _printDocument = null;
        }
    }

    /// <summary>
    ///  Gets or sets the printer settings the dialog box will modify if the user clicks the Printer button.
    /// </summary>
    [SRCategory(nameof(SR.CatData))]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [SRDescription(nameof(SR.PSDprinterSettingsDescr))]
    public PrinterSettings? PrinterSettings
    {
        get => _printerSettings;
        set
        {
            _printerSettings = value;
            _printDocument = null;
        }
    }

    /// <summary>
    ///  Gets or sets a value indicating whether the Help button is visible.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(false)]
    [SRDescription(nameof(SR.PSDshowHelpDescr))]
    public bool ShowHelp { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating whether the Network button is visible.
    /// </summary>
    [SRCategory(nameof(SR.CatBehavior))]
    [DefaultValue(true)]
    [SRDescription(nameof(SR.PSDshowNetworkDescr))]
    public bool ShowNetwork { get; set; }

    private PAGESETUPDLG_FLAGS GetFlags()
    {
        PAGESETUPDLG_FLAGS flags = PAGESETUPDLG_FLAGS.PSD_ENABLEPAGESETUPHOOK;

        if (!AllowMargins)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_DISABLEMARGINS;
        }

        if (!AllowOrientation)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_DISABLEORIENTATION;
        }

        if (!AllowPaper)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_DISABLEPAPER;
        }

        if (!AllowPrinter || _printerSettings is null)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_DISABLEPRINTER;
        }

        if (ShowHelp)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_SHOWHELP;
        }

        if (!ShowNetwork)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_NONETWORKBUTTON;
        }

        if (_minMargins is not null)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_MINMARGINS;
        }

        if (_pageSettings?.Margins is not null)
        {
            flags |= PAGESETUPDLG_FLAGS.PSD_MARGINS;
        }

        return flags;
    }

    /// <summary>
    ///  Resets all options to their default values.
    /// </summary>
    public override void Reset()
    {
        AllowMargins = true;
        AllowOrientation = true;
        AllowPaper = true;
        AllowPrinter = true;
        MinMargins = null; // turns into Margin with all zeros
        _pageSettings = null;
        _printDocument = null;
        _printerSettings = null;
        ShowHelp = false;
        ShowNetwork = true;
    }

    // The next two methods are for designer support.

    private void ResetMinMargins() => MinMargins = null;

    /// <summary>
    ///  Indicates whether the <see cref="MinMargins"/> property should be persisted.
    /// </summary>
    private bool ShouldSerializeMinMargins() =>
        _minMargins is not null
            && (_minMargins.Left != 0
            || _minMargins.Right != 0
            || _minMargins.Top != 0
            || _minMargins.Bottom != 0);

    private static void UpdateSettings(
        PAGESETUPDLGW data,
        PageSettings pageSettings,
        PrinterSettings? printerSettings)
    {
        pageSettings.SetHdevmode(data.hDevMode);
        if (printerSettings is not null)
        {
            printerSettings.SetHdevmode(data.hDevMode);
            printerSettings.SetHdevnames(data.hDevNames);
        }

        Margins newMargins = new()
        {
            Left = data.rtMargin.left,
            Top = data.rtMargin.top,
            Right = data.rtMargin.right,
            Bottom = data.rtMargin.bottom
        };

        PrinterUnit fromUnit = ((data.Flags & PAGESETUPDLG_FLAGS.PSD_INHUNDREDTHSOFMILLIMETERS) != 0)
            ? PrinterUnit.HundredthsOfAMillimeter
            : PrinterUnit.ThousandthsOfAnInch;

        pageSettings.Margins = PrinterUnitConvert.Convert(newMargins, fromUnit, PrinterUnit.Display);
    }

    protected override unsafe bool RunDialog(IntPtr hwndOwner)
    {
        if (_pageSettings is null)
        {
            throw new ArgumentException(SR.PSDcantShowWithoutPage);
        }

        if (Graphics.IsBackendActive)
        {
            using ManagedPageSetupDialog dialog = new((nint)hwndOwner, this, _pageSettings);
            return dialog.ShowDialog(new WindowWrapper(hwndOwner)) == DialogResult.OK;
        }

        PAGESETUPDLGW dialogSettings = new()
        {
            lStructSize = (uint)sizeof(PAGESETUPDLGW),
            Flags = GetFlags(),
            hwndOwner = (HWND)hwndOwner,
            lpfnPageSetupHook = HookProcFunctionPointer
        };

        PrinterUnit toUnit = PrinterUnit.ThousandthsOfAnInch;

        // EnableMetric allows the users to choose between the AutoConversion or not.
        if (EnableMetric)
        {
            // Take the Units of Measurement while determining the Printer Units.
            Span<char> buffer = stackalloc char[2];
            int result;
            fixed (char* pBuffer = buffer)
            {
                result = PInvoke.GetLocaleInfoEx(
                    PInvoke.LOCALE_NAME_SYSTEM_DEFAULT,
                    PInvoke.LOCALE_IMEASURE,
                    pBuffer,
                    buffer.Length);
            }

            if (result > 0 && int.Parse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture) == 0)
            {
                toUnit = PrinterUnit.HundredthsOfAMillimeter;
            }
        }

        if (MinMargins is not null)
        {
            Margins margins = PrinterUnitConvert.Convert(MinMargins, PrinterUnit.Display, toUnit);
            dialogSettings.rtMinMargin.left = margins.Left;
            dialogSettings.rtMinMargin.top = margins.Top;
            dialogSettings.rtMinMargin.right = margins.Right;
            dialogSettings.rtMinMargin.bottom = margins.Bottom;
        }

        if (_pageSettings.Margins is not null)
        {
            Margins margins = PrinterUnitConvert.Convert(_pageSettings.Margins, PrinterUnit.Display, toUnit);
            dialogSettings.rtMargin.left = margins.Left;
            dialogSettings.rtMargin.top = margins.Top;
            dialogSettings.rtMargin.right = margins.Right;
            dialogSettings.rtMargin.bottom = margins.Bottom;
        }

        // Ensure that the margins are >= minMargins.
        // This is a requirement of the PAGESETUPDLG structure.
        dialogSettings.rtMargin.left = Math.Max(dialogSettings.rtMargin.left, dialogSettings.rtMinMargin.left);
        dialogSettings.rtMargin.top = Math.Max(dialogSettings.rtMargin.top, dialogSettings.rtMinMargin.top);
        dialogSettings.rtMargin.right = Math.Max(dialogSettings.rtMargin.right, dialogSettings.rtMinMargin.right);
        dialogSettings.rtMargin.bottom = Math.Max(dialogSettings.rtMargin.bottom, dialogSettings.rtMinMargin.bottom);

        PrinterSettings printer = _printerSettings ?? _pageSettings.PrinterSettings;

        dialogSettings.hDevMode = (HGLOBAL)printer.GetHdevmode(_pageSettings);
        dialogSettings.hDevNames = (HGLOBAL)printer.GetHdevnames();

        try
        {
            if (!PInvoke.PageSetupDlg(&dialogSettings))
            {
                return false;
            }

            // PrinterSettings, not printer
            UpdateSettings(dialogSettings, _pageSettings, _printerSettings);
            return true;
        }
        finally
        {
            PInvoke.GlobalFree(dialogSettings.hDevMode);
            PInvoke.GlobalFree(dialogSettings.hDevNames);
        }
    }

    private static void NotifyOwnerIdle(nint owner, nint dialogHandle)
    {
        if (owner == 0 || dialogHandle == 0)
        {
            return;
        }

        PInvoke.PostMessage((HWND)owner, PInvoke.WM_ENTERIDLE, (WPARAM)0, (LPARAM)dialogHandle);
    }

    private sealed class WindowWrapper(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    private sealed class ManagedPageSetupDialog : Form
    {
        private readonly nint _owner;
        private readonly PageSetupDialog _ownerDialog;
        private readonly PageSettings _pageSettings;
        private readonly NumericUpDown _leftMargin = CreateMarginInput();
        private readonly NumericUpDown _rightMargin = CreateMarginInput();
        private readonly NumericUpDown _topMargin = CreateMarginInput();
        private readonly NumericUpDown _bottomMargin = CreateMarginInput();
        private readonly CheckBox _landscape = new() { AutoSize = true, Text = "Landscape" };

        public ManagedPageSetupDialog(nint owner, PageSetupDialog ownerDialog, PageSettings pageSettings)
        {
            _owner = owner;
            _ownerDialog = ownerDialog;
            _pageSettings = pageSettings;

            Text = "Page Setup";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(420, 250);
            MinimumSize = new Size(360, 220);

            InitializeValues();
            Controls.Add(CreateContent());
        }

        protected override void OnShown(EventArgs e)
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

        private void InitializeValues()
        {
            Margins margins = _pageSettings.Margins;
            _leftMargin.Value = ClampMargin(margins.Left, _ownerDialog.MinMargins?.Left ?? 0);
            _rightMargin.Value = ClampMargin(margins.Right, _ownerDialog.MinMargins?.Right ?? 0);
            _topMargin.Value = ClampMargin(margins.Top, _ownerDialog.MinMargins?.Top ?? 0);
            _bottomMargin.Value = ClampMargin(margins.Bottom, _ownerDialog.MinMargins?.Bottom ?? 0);

            _landscape.Checked = _pageSettings.Landscape;
            _landscape.Enabled = _ownerDialog.AllowOrientation;

            bool marginsEnabled = _ownerDialog.AllowMargins;
            _leftMargin.Enabled = marginsEnabled;
            _rightMargin.Enabled = marginsEnabled;
            _topMargin.Enabled = marginsEnabled;
            _bottomMargin.Enabled = marginsEnabled;
        }

        private TableLayoutPanel CreateContent()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = new()
            {
                AutoSize = true,
                Text = "Page settings",
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(title, 0, 0);

            TableLayoutPanel fields = new()
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true
            };

            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddMarginRow(fields, "Left:", _leftMargin);
            AddMarginRow(fields, "Right:", _rightMargin);
            AddMarginRow(fields, "Top:", _topMargin);
            AddMarginRow(fields, "Bottom:", _bottomMargin);
            fields.Controls.Add(new Label { AutoSize = true, Text = "Orientation:" }, 0, fields.RowCount);
            fields.Controls.Add(_landscape, 1, fields.RowCount++);
            root.Controls.Add(fields, 0, 1);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            Button ok = new() { Text = "OK", AutoSize = true, DialogResult = DialogResult.None };
            ok.Click += (sender, e) => AcceptDialog();
            Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;
            root.Controls.Add(buttons, 0, 2);

            return root;
        }

        private void AcceptDialog()
        {
            _pageSettings.Margins = new Margins(
                (int)_leftMargin.Value,
                (int)_rightMargin.Value,
                (int)_topMargin.Value,
                (int)_bottomMargin.Value);

            if (_ownerDialog.AllowOrientation)
            {
                _pageSettings.Landscape = _landscape.Checked;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void AddMarginRow(TableLayoutPanel fields, string label, NumericUpDown input)
        {
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label { AutoSize = true, Text = label, Padding = new Padding(0, 4, 8, 0) }, 0, row);
            fields.Controls.Add(input, 1, row);
        }

        private static NumericUpDown CreateMarginInput()
            => new()
            {
                Minimum = 0,
                Maximum = 10000,
                Increment = 10,
                Width = 120
            };

        private static decimal ClampMargin(int value, int min)
            => Math.Clamp(value, Math.Max(0, min), 10000);
    }
}
