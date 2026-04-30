// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Windows.Forms.Design;
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

    [UIFact]
    public void MaskDesignerDialog_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using MaskedTextBox maskedTextBox = new()
        {
            Mask = "00/00/0000"
        };

        using MaskDesignerDialog dialog = new(maskedTextBox, helpService: null);

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void MaskDesignerDialog_ShowDialog_OkButton_ReturnsOk()
    {
        using MaskedTextBox maskedTextBox = new()
        {
            Mask = "00/00/0000"
        };

        using MaskDesignerDialog dialog = new(maskedTextBox, helpService: null);
        dialog.Shown += (sender, e) =>
        {
            Button okButton = GetPrivateField<Button>(dialog, "_btnOK");
            okButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
    }

    [UIFact]
    public void FormatStringDialog_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using ComboBox listControl = new()
        {
            FormatString = "N2"
        };

        using FormatStringDialog dialog = new(context: null)
        {
            ListControl = listControl
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void FormatStringDialog_ShowDialog_OkButton_ReturnsOk()
    {
        using ComboBox listControl = new()
        {
            FormatString = "N2"
        };

        using FormatStringDialog dialog = new(context: null)
        {
            ListControl = listControl
        };

        dialog.Shown += (sender, e) =>
        {
            Button okButton = GetPrivateField<Button>(dialog, "_okButton");
            okButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
    }

    [UIFact]
    public void StringCollectionEditor_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using Form dialog = CreateStringCollectionEditorForm(["one"]);

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void StringCollectionEditor_ShowDialog_OkButton_ReturnsOk()
    {
        using Form dialog = CreateStringCollectionEditorForm(["one"]);
        dialog.Shown += (sender, e) =>
        {
            TextBox textEntry = GetPrivateField<TextBox>(dialog, "_textEntry");
            Button okButton = GetPrivateField<Button>(dialog, "_okButton");

            textEntry.Text = "one" + Environment.NewLine + "two";
            okButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
    }

    [UIFact]
    public void DataGridViewColumnCollectionDialog_ShowDialog_CloseFromOwner_ReturnsCancel()
    {
        using DialogHostForm dialogOwnerForm = new();
        using DataGridView liveDataGridView = CreateLiveDataGridViewForColumnDialog();
        using DataGridViewColumnCollectionDialog dialog = CreateDataGridViewColumnCollectionDialog(liveDataGridView);

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void DataGridViewColumnCollectionDialog_ShowDialog_OkButton_ReturnsOk()
    {
        using DataGridView liveDataGridView = CreateLiveDataGridViewForColumnDialog();
        using DataGridViewColumnCollectionDialog dialog = CreateDataGridViewColumnCollectionDialog(liveDataGridView);

        dialog.Shown += (sender, e) =>
        {
            Button okButton = GetPrivateField<Button>(dialog, "_okButton");
            okButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Single(liveDataGridView.Columns);
    }

    private static MdiWindowDialog CreateMdiWindowDialog(Form first, Form second)
    {
        MdiWindowDialog dialog = new();
        dialog.SetItems(first, [first, second]);
        return dialog;
    }

    private static Form CreateStringCollectionEditorForm(string[] values)
    {
        TestStringCollectionEditor editor = new();
        return editor.CreateForm(values);
    }

    private static DataGridView CreateLiveDataGridViewForColumnDialog()
    {
        DataGridView dataGridView = new();
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            Name = "nameColumn"
        });

        return dataGridView;
    }

    private static DataGridViewColumnCollectionDialog CreateDataGridViewColumnCollectionDialog(
        DataGridView liveDataGridView)
    {
        DataGridViewColumnCollectionDialog dialog = new();
        dialog.SetLiveDataGridView(liveDataGridView);
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

    private sealed class TestStringCollectionEditor : StringCollectionEditor
    {
        public TestStringCollectionEditor()
            : base(typeof(string[]))
        {
        }

        public Form CreateForm(string[] values)
        {
            CollectionForm form = CreateCollectionForm();
            form.EditValue = values;
            return form;
        }
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
