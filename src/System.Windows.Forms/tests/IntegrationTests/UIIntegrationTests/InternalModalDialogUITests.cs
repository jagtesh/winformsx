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
}
