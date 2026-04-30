// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Windows.Forms.PropertyGridInternal;
using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class InternalModalDialogUITests : ControlTestBase
{
    public InternalModalDialogUITests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void ThreadExceptionDialog_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using ThreadExceptionDialog dialog = new(new InvalidOperationException("Thread exception smoke"));

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void ThreadExceptionDialog_DetailsButton_ExpandsDetails()
    {
        using ThreadExceptionDialog dialog = new(new InvalidOperationException("Thread exception details"));
        dialog.Shown += (sender, e) =>
        {
            Button detailsButton = GetPrivateField<Button>(dialog, "_detailsButton");
            TextBox detailsTextBox = GetPrivateField<TextBox>(dialog, "_details");

            Assert.False(detailsTextBox.Visible);
            detailsButton.PerformClick();
            Assert.True(detailsTextBox.Visible);
            dialog.Close();
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
    }

    [UIFact]
    public void GridErrorDialog_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using PropertyGrid ownerGrid = new();
        using GridErrorDialog dialog = CreateGridErrorDialog(ownerGrid);

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void GridErrorDialog_DetailsButton_ExpandsDetails()
    {
        using PropertyGrid ownerGrid = new();
        using GridErrorDialog dialog = CreateGridErrorDialog(ownerGrid);
        dialog.Shown += (sender, e) =>
        {
            Button detailsButton = (Button)dialog.Controls.Find("detailsBtn", searchAllChildren: true).Single();
            TextBox detailsTextBox = (TextBox)dialog.Controls.Find("details", searchAllChildren: true).Single();

            Assert.False(detailsTextBox.Visible);
            detailsButton.PerformClick();
            Assert.True(detailsTextBox.Visible);
            Assert.True(dialog.DetailsButtonExpanded);
            dialog.Close();
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
    }

    [UIFact]
    public void MdiWindowDialog_ShowDialog_CancelButton_ReturnsCancel()
    {
        using MdiWindowDialogHost host = new();
        using MdiWindowDialog dialog = CreateMdiWindowDialog(host.First, host.Second);
        dialog.Shown += (sender, e) =>
        {
            Button cancelButton = (Button)dialog.Controls.Find("cancelButton", searchAllChildren: true).Single();
            cancelButton.PerformClick();
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
    }

    [UIFact]
    public void MdiWindowDialog_ShowDialog_OkButton_ReturnsSelectedChild()
    {
        using MdiWindowDialogHost host = new();
        using MdiWindowDialog dialog = CreateMdiWindowDialog(host.First, host.Second);
        dialog.Shown += (sender, e) =>
        {
            ListBox itemList = (ListBox)dialog.Controls.Find("itemList", searchAllChildren: true).Single();
            Button okButton = (Button)dialog.Controls.Find("okButton", searchAllChildren: true).Single();

            itemList.SelectedIndex = 1;
            okButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Same(host.Second, dialog.ActiveChildForm);
    }

    private static MdiWindowDialog CreateMdiWindowDialog(Form first, Form second)
    {
        MdiWindowDialog dialog = new();
        dialog.SetItems(first, [first, second]);
        return dialog;
    }

    private static GridErrorDialog CreateGridErrorDialog(PropertyGrid ownerGrid)
    {
        GridErrorDialog dialog = new(ownerGrid)
        {
            Message = "Property value could not be applied.",
            Details = "The setter threw during validation."
        };

        return dialog;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);

        return (T)field.GetValue(instance)!;
    }

    private sealed class MdiWindowDialogHost : IDisposable
    {
        public MdiWindowDialogHost()
        {
            Parent = new()
            {
                IsMdiContainer = true,
                Text = "MDI parent"
            };

            First = new()
            {
                MdiParent = Parent,
                Text = "First child"
            };

            Second = new()
            {
                MdiParent = Parent,
                Text = "Second child"
            };

            Parent.Show();
            First.Show();
            Second.Show();
            First.Activate();
        }

        public Form Parent { get; }

        public Form First { get; }

        public Form Second { get; }

        public void Dispose()
        {
            Second.Dispose();
            First.Dispose();
            Parent.Dispose();
        }
    }
}
