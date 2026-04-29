// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms.IntegrationTests.Common;
using Microsoft.Win32;

namespace WinFormsControlsTest;

[DesignerCategory("code")]
public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();

        // Init buttons
        IReadOnlyDictionary<MainFormControlsTabOrder, InitInfo> buttonsInitInfo = GetButtonsInitInfo();
        Array mainFormControlsTabOrderItems = Enum.GetValues(typeof(MainFormControlsTabOrder));

        foreach (MainFormControlsTabOrder item in mainFormControlsTabOrderItems)
        {
            InitInfo info = buttonsInitInfo[item];
            Button button = new Button
            {
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Name = info.Name,
                TabIndex = (int)item,
                Text = info.Name,
                UseVisualStyleBackColor = true
            };
            button.Click += info.Click;

            overarchingFlowLayoutPanel.Controls.Add(button);
        }

        Text = RuntimeInformation.FrameworkDescription;

        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            // The default font gets reset for UserPreferenceCategory.Color
            // though perhaps it should've been done for UserPreferenceCategory.Window
            if (e.Category == UserPreferenceCategory.Color)
            {
                UpdateLayout();
            }
        };

        UpdateLayout();
    }

    private static IReadOnlyDictionary<MainFormControlsTabOrder, InitInfo> GetButtonsInitInfo()
    {
        Dictionary<MainFormControlsTabOrder, InitInfo> initInfo = ControlSmokeTestCatalog.TestCases.ToDictionary(
            testCase => testCase.TabOrder,
            testCase => new InitInfo(testCase.Name, (obj, e) =>
            {
                Control owner = (Control)obj!;
                Form ownerForm = owner.FindForm();
                if (!testCase.IsSupported)
                {
                    MessageBox.Show(ownerForm, testCase.SkipReason, testCase.Name);
                    return;
                }

                Form form = testCase.CreateForm();
                if (testCase.ShowModalFromMainForm)
                {
                    form.ShowDialog(ownerForm);
                }
                else
                {
                    form.Show(ownerForm);
                }
            }));

        initInfo[MainFormControlsTabOrder.ToggleIconButton] = new InitInfo("ToggleFormIcon", (obj, e) =>
        {
            Form form = ((Control)obj!).FindForm()!;
            form.ShowIcon = !form.ShowIcon;
        });

        return initInfo;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        UpdateLayout();
        overarchingFlowLayoutPanel.Controls[(int)MainFormControlsTabOrder.ButtonsButton].Focus();
    }

    private void UpdateLayout()
    {
        MinimumSize = default;
        Debug.WriteLine($"MessageBoxFont: {SystemFonts.MessageBoxFont}", nameof(MainForm));
        Debug.WriteLine($"Default font: {Control.DefaultFont}", nameof(MainForm));

        List<Button> buttons = [];
        foreach (Control control in overarchingFlowLayoutPanel.Controls)
        {
            if (control is Button button)
            {
                buttons.Add(button);
            }
            else
            {
                Debug.WriteLine($"Why did we get a {control.GetType().Name} instead a {nameof(Button)} on {nameof(MainForm)}?");
            }
        }

        Size biggestButton = default;
        foreach (Button button in buttons)
        {
            Size preferredSize = button.GetPreferredSize(Size.Empty);
            if (preferredSize.Width > biggestButton.Width)
            {
                biggestButton.Width = preferredSize.Width;
            }

            if (preferredSize.Height > biggestButton.Height)
            {
                biggestButton.Height = preferredSize.Height;
            }
        }

        Debug.WriteLine($"Biggest button size: {biggestButton}", nameof(MainForm));

        // Size the host first so a top-down FlowLayoutPanel wraps into columns on the first paint.
        int padding = overarchingFlowLayoutPanel.Controls[0].Margin.All;
        int columns = 3;
        int rows = (int)Math.Ceiling(overarchingFlowLayoutPanel.Controls.Count / (double)columns);
        int panelWidth = columns * (biggestButton.Width + padding * 2) + padding * 2;
        int panelHeight = rows * (biggestButton.Height + padding * 2) + padding * 2;
        ClientSize = new Size(panelWidth + Padding.Horizontal, panelHeight + Padding.Vertical);

        overarchingFlowLayoutPanel.SuspendLayout();
        foreach (Button button in buttons)
        {
            button.AutoSize = false;
            button.Size = biggestButton;
        }

        overarchingFlowLayoutPanel.Size = new Size(panelWidth, panelHeight);
        overarchingFlowLayoutPanel.ResumeLayout(true);
        PerformLayout();

        MinimumSize = Size;
        Debug.WriteLine($"Minimum form size: {MinimumSize}", nameof(MainForm));
    }

    private struct InitInfo
    {
        public InitInfo(string name, EventHandler handler)
        {
            Name = name;
            Click = handler;
        }

        public string Name { get; }

        public EventHandler Click { get; }
    }
}
