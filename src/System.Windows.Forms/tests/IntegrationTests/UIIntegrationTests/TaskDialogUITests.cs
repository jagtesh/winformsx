// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
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

    [UIFact]
    public void TaskDialog_ShowDialog_CommandLinkClick_ReturnsButton()
    {
        TaskDialogCommandLinkButton commandLink = new("Primary action", "Command link detail");
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_CommandLinkClick_ReturnsButton),
            Heading = "Choose"
        };

        page.Buttons.Add(commandLink);
        page.Created += (sender, e) =>
        {
            Button visibleCommandLink = FindOpenTaskDialogControl<Button>(
                page.Caption,
                button => button.Text.Contains("Command link detail", StringComparison.Ordinal));

            Assert.Contains('\n', visibleCommandLink.Text);
            visibleCommandLink.PerformClick();
        };

        Assert.Equal(commandLink, TaskDialog.ShowDialog(page));
    }

    [UIFact]
    public void TaskDialog_ShowDialog_LinkClick_RaisesLinkClicked()
    {
        string? clickedHref = null;
        TaskDialogPage page = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_LinkClick_RaisesLinkClicked),
            EnableLinks = true,
            Text = "Open <a href=\"winformsx://docs\">docs</a>"
        };

        page.LinkClicked += (sender, e) =>
        {
            clickedHref = e.LinkHref;
            page.BoundDialog!.Close();
        };

        page.Created += (sender, e) =>
        {
            LinkLabel visibleContent = FindOpenTaskDialogControl<LinkLabel>(
                page.Caption,
                label => label.Text == "Open docs" && label.Links.Count == 1);

            MethodInfo onLinkClicked = typeof(LinkLabel).GetMethod(
                "OnLinkClicked",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            onLinkClicked.Invoke(visibleContent, [new LinkLabelLinkClickedEventArgs(visibleContent.Links[0])]);
        };

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(page));
        Assert.Equal("winformsx://docs", clickedHref);
    }

    [UIFact]
    public void TaskDialog_ShowDialog_Navigate_RebuildsVisiblePage()
    {
        bool firstDestroyed = false;
        bool secondCreated = false;

        TaskDialogPage first = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_Navigate_RebuildsVisiblePage),
            Heading = "First",
            Text = "First content"
        };

        TaskDialogPage second = new()
        {
            Caption = nameof(TaskDialog_ShowDialog_Navigate_RebuildsVisiblePage),
            Heading = "Second",
            Text = "Second content"
        };

        first.Created += (sender, e) => first.Navigate(second);
        first.Destroyed += (sender, e) => firstDestroyed = true;
        second.Created += (sender, e) =>
        {
            secondCreated = true;
            FindOpenTaskDialogControl<LinkLabel>(
                second.Caption,
                label => label.Text == "Second content");
            second.BoundDialog!.Close();
        };

        Assert.Equal(TaskDialogButton.Cancel, TaskDialog.ShowDialog(first));
        Assert.True(firstDestroyed);
        Assert.True(secondCreated);
    }

    private static T FindOpenTaskDialogControl<T>(string? caption, Predicate<T>? match = null) where T : Control
    {
        foreach (Form form in Application.OpenForms)
        {
            if (form.Text == caption && FindControl(form, match) is { } control)
            {
                return control;
            }
        }

        throw new InvalidOperationException($"Could not find visible {typeof(T).Name} in the open task dialog.");
    }

    private static T? FindControl<T>(Control parent, Predicate<T>? match = null) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T typedChild && match?.Invoke(typedChild) != false)
            {
                return typedChild;
            }

            if (FindControl(child, match) is { } nestedMatch)
            {
                return nestedMatch;
            }
        }

        return null;
    }
}
