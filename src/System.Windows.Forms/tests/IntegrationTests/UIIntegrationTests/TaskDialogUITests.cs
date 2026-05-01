// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class TaskDialogUITests : ControlTestBase
{
    public TaskDialogUITests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void TaskDialog_ShowDialog_CloseFromCreated_ReturnsCancel()
    {
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_CloseFromCreated_ReturnsCancel),
            Heading = "Heading",
            Text = "Content"
        };

        page.Created += (sender, e) => page.BoundDialog!.Close();

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(page));
    }

    [UIFact]
    public void TaskDialog_ShowDialog_PerformStandardButtonClick_ReturnsButton()
    {
        TaskDialogButton yesButton = TaskDialogButton.Yes;
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_PerformStandardButtonClick_ReturnsButton),
            Heading = "Choose",
            Text = "Continue?"
        };

        page.Buttons.Add(yesButton);
        page.Buttons.Add(TaskDialogButton.No);
        page.Created += (sender, e) => yesButton.PerformClick();

        Assert.Equal(TaskDialogButton.Yes, TaskDialog.ShowDialog(page));
    }

    [UIFact]
    public void TaskDialog_ShowDialog_VerificationCanUpdateWhileShown()
    {
        TaskDialogVerificationCheckBox verification = new("Remember", isChecked: false);
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_VerificationCanUpdateWhileShown),
            Verification = verification
        };

        page.Created += (sender, e) =>
        {
            verification.Checked = true;
            page.BoundDialog!.Close();
        };

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(page));
        Assert.True(verification.Checked);
    }

    [UIFact]
    public void TaskDialog_ShowDialog_RadioButtonCanUpdateWhileShown()
    {
        TaskDialogRadioButton first = new("First") { Checked = true };
        TaskDialogRadioButton second = new("Second");
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_RadioButtonCanUpdateWhileShown)
        };

        page.RadioButtons.Add(first);
        page.RadioButtons.Add(second);
        page.Created += (sender, e) =>
        {
            second.Checked = true;
            page.BoundDialog!.Close();
        };

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(page));
        Assert.False(first.Checked);
        Assert.True(second.Checked);
    }

    [UIFact]
    public void TaskDialog_ShowDialog_ProgressBarCanUpdateVisibleControlWhileShown()
    {
        TaskDialogProgressBar progressBar = new(TaskDialogProgressBarState.Normal)
        {
            Minimum = 10,
            Maximum = 90,
            Value = 20
        };

        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_ProgressBarCanUpdateVisibleControlWhileShown),
            ProgressBar = progressBar
        };

        page.Created += (sender, e) =>
        {
            progressBar.Value = 42;
            ProgressBar visibleProgressBar = FindOpenTaskDialogControl<ProgressBar>(page.Caption);

            Assert.Equal(10, visibleProgressBar.Minimum);
            Assert.Equal(90, visibleProgressBar.Maximum);
            Assert.Equal(42, visibleProgressBar.Value);

            page.BoundDialog!.Close();
        };

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(page));
        Assert.Equal(42, progressBar.Value);
    }

    private static T FindOpenTaskDialogControl<T>(string? caption) where T : Control
    {
        foreach (Form form in Application.OpenForms)
        {
            if (form.Text == caption && FindControl<T>(form) is { } control)
            {
                return control;
            }
        }

        throw new InvalidOperationException($"Could not find visible {typeof(T).Name} in the open task dialog.");
    }

    private static T? FindControl<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match)
            {
                return match;
            }

            if (FindControl<T>(child) is { } nestedMatch)
            {
                return nestedMatch;
            }
        }

        return null;
    }
}
