// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.IntegrationTests.Common;
using WindowsFormsApp1;
using WinFormsControlsTest.UserControls;

namespace WinFormsControlsTest;

internal static class ControlSmokeTestCatalog
{
    public static IReadOnlyList<ControlSmokeTestCase> TestCases { get; } =
    [
        new(MainFormControlsTabOrder.ButtonsButton, "Buttons", () => new Buttons()),
        new(MainFormControlsTabOrder.CalendarButton, "Calendar", () => new Calendar()),
        new(MainFormControlsTabOrder.MultipleControlsButton, "MultipleControls", () => new MultipleControls()),
        new(MainFormControlsTabOrder.ComboBoxesButton, "ComboBoxes", () => new ComboBoxes()),
        new(MainFormControlsTabOrder.ComboBoxesWithScrollBarsButton, "ComboBoxes with ScrollBars", () => new ComboBoxesWithScrollBars()),
        new(MainFormControlsTabOrder.DateTimePickerButton, "DateTimePicker", () => new DateTimePicker()),
        new(MainFormControlsTabOrder.DialogsButton, "Dialogs", () => new Dialogs(), showModalFromMainForm: true),
        new(MainFormControlsTabOrder.DataGridViewButton, "DataGridView", () => new DataGridViewTest()),
        new(MainFormControlsTabOrder.DataGridViewInVirtualModeButton, "DataGridView in Virtual mode", () => new DataGridViewInVirtualModeTest()),
        new(MainFormControlsTabOrder.TreeViewButton, "TreeView, ImageList", () => new TreeViewTest()),
        new(MainFormControlsTabOrder.ContentAlignmentButton, "ContentAlignment", () => new DesignTimeAligned()),
        new(MainFormControlsTabOrder.MenusButton, "Menus", () => new MenuStripAndCheckedListBox()),
        new(MainFormControlsTabOrder.PanelsButton, "Panels", () => new Panels()),
        new(MainFormControlsTabOrder.SplitterButton, "Splitter", () => new Splitter()),
        new(MainFormControlsTabOrder.MdiParentButton, "MDI Parent", () => new MdiParent()),
        new(MainFormControlsTabOrder.PropertyGridButton, "PropertyGrid", () => new PropertyGrid(new UserControlWithObjectCollectionEditor())),
        new(MainFormControlsTabOrder.ListViewButton, "ListView", () => new ListViewTest()),
        new(MainFormControlsTabOrder.FontNameEditorButton, "FontNameEditor", () => new PropertyGrid(new UserControlWithFontNameEditor())),
        new(MainFormControlsTabOrder.CollectionEditorsButton, "CollectionEditors", () => new CollectionEditors()),
        new(MainFormControlsTabOrder.RichTextBoxesButton, "RichTextBoxes", () => new RichTextBoxes()),
        new(MainFormControlsTabOrder.PictureBoxesButton, "PictureBoxes", () => new PictureBoxes()),
        new(MainFormControlsTabOrder.FormBorderStylesButton, "FormBorderStyles", () => new FormBorderStyles()),
        new(MainFormControlsTabOrder.FormShowInTaskbarButton, "FormShowInTaskbar", () => new FormShowInTaskbar()),
        new(MainFormControlsTabOrder.ErrorProviderButton, "ErrorProvider", () => new ErrorProviderTest()),
        new(MainFormControlsTabOrder.TaskDialogButton, "Task Dialog", () => new TaskDialogSamples()),
        new(MainFormControlsTabOrder.MessageBoxButton, "MessageBox", () => new MessageBoxes()),
        new(MainFormControlsTabOrder.ToolStripsButton, "ToolStrips", () => new ToolStripTests()),
        new(MainFormControlsTabOrder.TrackBarsButton, "TrackBars", () => new TrackBars()),
        new(MainFormControlsTabOrder.ScrollBarsButton, "ScrollBars", () => new ScrollBars()),
        new(MainFormControlsTabOrder.ToolTipsButton, "ToolTips", () => new ToolTipTests()),
        new(MainFormControlsTabOrder.AnchorLayoutButton, "AnchorLayout", () => new AnchorLayoutTests()),
        new(MainFormControlsTabOrder.DockLayoutButton, "DockLayout", () => new DockLayoutTests()),
        new(MainFormControlsTabOrder.DragAndDrop, "Drag and Drop", () => new DragDrop()),
        new(MainFormControlsTabOrder.TextBoxesButton, "TextBoxes", () => new TextBoxes()),
#if WINDOWS
        new(MainFormControlsTabOrder.MediaPlayerButton, "MediaPlayer", () => new MediaPlayer()),
#else
        ControlSmokeTestCase.Skip(MainFormControlsTabOrder.MediaPlayerButton, "MediaPlayer", "Windows Media Player ActiveX is Windows-only."),
#endif
        new(MainFormControlsTabOrder.FormOwnerTestButton, "FormOwnerTest", () => new FormOwnerTestForm()),
        new(MainFormControlsTabOrder.ListBoxTestButton, "ListBoxes", () => new ListBoxes()),
        new(MainFormControlsTabOrder.PasswordButton, "Password", () => new Password()),
        new(MainFormControlsTabOrder.ChartControlButton, "ChartControl", () => new ChartControl()),
        new(MainFormControlsTabOrder.ToolStripSeparatorPreferredSize, "ToolStripSeparatorPreferredSize", () => new ToolStripSeparatorPreferredSize()),
        new(MainFormControlsTabOrder.CustomComCtl32Button, "ComCtl32 Button Custom Border", () => new CustomComCtl32Button()),
        new(MainFormControlsTabOrder.ScrollableControlsButton, "ScrollableControlsButton", () => new ScrollableControls()),
    ];

    public static IReadOnlyDictionary<MainFormControlsTabOrder, ControlSmokeTestCase> TestCasesByTabOrder { get; } =
        TestCases.ToDictionary(testCase => testCase.TabOrder);
}

internal sealed class ControlSmokeTestCase(
    MainFormControlsTabOrder tabOrder,
    string name,
    Func<Form> createForm,
    bool showModalFromMainForm = false,
    string skipReason = "")
{
    public MainFormControlsTabOrder TabOrder { get; } = tabOrder;

    public string Name { get; } = name;

    public Func<Form> CreateForm { get; } = createForm;

    public bool ShowModalFromMainForm { get; } = showModalFromMainForm;

    public string SkipReason { get; } = skipReason;

    public bool IsSupported => SkipReason.Length == 0;

    public static ControlSmokeTestCase Skip(
        MainFormControlsTabOrder tabOrder,
        string name,
        string skipReason) =>
        new(tabOrder, name, () => throw new PlatformNotSupportedException(skipReason), skipReason: skipReason);
}
