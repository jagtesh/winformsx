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

        // 1. Auto-size all buttons
        overarchingFlowLayoutPanel.SuspendLayout();
        foreach (Button button in buttons)
        {
            button.AutoSize = true;
        }

        overarchingFlowLayoutPanel.ResumeLayout(true);

        // 2. Find the biggest button
        Size biggestButton = default;
        foreach (Button button in buttons)
        {
            if (button.Width > biggestButton.Width)
            {
                biggestButton = button.Size;
            }
        }

        Debug.WriteLine($"Biggest button size: {biggestButton}", nameof(MainForm));

        // 3. Size all buttons to the biggest button
        overarchingFlowLayoutPanel.SuspendLayout();
        foreach (Button button in buttons)
        {
            button.AutoSize = false;
            button.Size = biggestButton;
        }

        overarchingFlowLayoutPanel.ResumeLayout(true);

        // 4. Calculate the new form size showing all buttons in three vertical columns
        int padding = overarchingFlowLayoutPanel.Controls[0].Margin.All;

        ClientSize = new Size(
            (biggestButton.Width + padding * 2) * 3 + padding * 2 + overarchingFlowLayoutPanel.Location.X * 2,
            (int)Math.Ceiling((overarchingFlowLayoutPanel.Controls.Count + 1) / 3.0) * (biggestButton.Height + padding * 2)
                + padding * 2 + overarchingFlowLayoutPanel.Location.Y * 2);
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
