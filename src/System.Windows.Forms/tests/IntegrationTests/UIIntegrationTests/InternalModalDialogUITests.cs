// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.ComponentModel.Design;
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

    [UIFact]
    public void DataGridViewAddColumnDialog_ShowDialog_CloseFromOwner_Completes()
    {
        using DialogHostForm dialogOwnerForm = new();
        using DataGridView liveDataGridView = CreateLiveDataGridViewForColumnDialog();
        using DataGridViewAddColumnDialog dialog = CreateDataGridViewAddColumnDialog(liveDataGridView);

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void DataGridViewAddColumnDialog_ShowDialog_AddButton_AddsColumn()
    {
        using DataGridView liveDataGridView = CreateLiveDataGridViewForColumnDialog();
        using DataGridViewAddColumnDialog dialog = CreateDataGridViewAddColumnDialog(liveDataGridView);

        dialog.Shown += (sender, e) =>
        {
            TextBox nameTextBox = GetPrivateField<TextBox>(dialog, "_nameTextBox");
            TextBox headerTextBox = GetPrivateField<TextBox>(dialog, "_headerTextBox");
            Button addButton = GetPrivateField<Button>(dialog, "_addButton");
            Button cancelButton = GetPrivateField<Button>(dialog, "_cancelButton");

            nameTextBox.Text = "addedColumn";
            headerTextBox.Text = "Added Column";
            addButton.PerformClick();
            cancelButton.PerformClick();
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Equal(2, liveDataGridView.Columns.Count);
        Assert.Equal("addedColumn", liveDataGridView.Columns[1].Name);
        Assert.Equal("Added Column", liveDataGridView.Columns[1].HeaderText);
    }

    [UIFact]
    public void TreeNodeCollectionEditor_EditValue_AddRoot_CommitsNode()
    {
        using TreeView treeView = new();
        TreeNodeCollectionEditor editor = new(treeView.Nodes.GetType());
        TestTypeDescriptorContext context = new(treeView);
        ModalEditorService editorService = new(dialog =>
        {
            Button addRootButton = GetPrivateField<Button>(dialog, "_btnAddRoot");
            Button okButton = GetPrivateField<Button>(dialog, "_okButton");

            addRootButton.PerformClick();
            okButton.PerformClick();
        });

        Assert.Same(treeView.Nodes, editor.EditValue(context, editorService, treeView.Nodes));
        Assert.Single(treeView.Nodes);
        Assert.Equal("Node0", treeView.Nodes[0].Name);
    }

    [UIFact]
    public void ListViewItemCollectionEditor_EditValue_AddButton_CommitsItem()
    {
        using ListView listView = new();
        ListViewItemCollectionEditor editor = new(listView.Items.GetType());
        TestTypeDescriptorContext context = new(listView);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(listView.Items, editor.EditValue(context, editorService, listView.Items));
        Assert.Single(listView.Items);
    }

    [UIFact]
    public void ListViewGroupCollectionEditor_EditValue_AddButton_CommitsGroup()
    {
        using ListView listView = new();
        ListViewGroupCollectionEditor editor = new(listView.Groups.GetType());
        TestTypeDescriptorContext context = new(listView);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(listView.Groups, editor.EditValue(context, editorService, listView.Groups));
        Assert.Single(listView.Groups);
        Assert.Equal("ListViewGroup1", listView.Groups[0].Name);
    }

    [UIFact]
    public void ColumnHeaderCollectionEditor_EditValue_AddButton_CommitsColumn()
    {
        using ListView listView = new();
        ColumnHeaderCollectionEditor editor = new(listView.Columns.GetType());
        TestTypeDescriptorContext context = new(listView);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(listView.Columns, editor.EditValue(context, editorService, listView.Columns));
        Assert.Single(listView.Columns);
    }

    [UIFact]
    public void ListViewSubItemCollectionEditor_EditValue_AddButton_CommitsSubItem()
    {
        using ListView listView = new();
        ListViewItem item = listView.Items.Add("Item");
        ListViewSubItemCollectionEditor editor = new(item.SubItems.GetType());
        TestTypeDescriptorContext context = new(listView);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(item.SubItems, editor.EditValue(context, editorService, item.SubItems));
        Assert.Equal(2, item.SubItems.Count);
    }

    [UIFact]
    public void TabPageCollectionEditor_EditValue_AddButton_CommitsPage()
    {
        using TabControl tabControl = new();
        TabPageCollectionEditor editor = new();
        TestTypeDescriptorContext context = new(tabControl);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(tabControl.TabPages, editor.EditValue(context, editorService, tabControl.TabPages));
        Assert.Single(tabControl.TabPages);
        Assert.True(tabControl.TabPages[0].UseVisualStyleBackColor);
    }

    [UIFact]
    public void StyleCollectionEditor_EditValue_AddButton_CommitsRowStyle()
    {
        using TableLayoutPanel panel = new();
        StyleCollectionEditor editor = new(panel.RowStyles.GetType());
        TestTypeDescriptorContext context = new(panel);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(panel.RowStyles, editor.EditValue(context, editorService, panel.RowStyles));
        Assert.Single(panel.RowStyles);
    }

    [UIFact]
    public void StyleCollectionEditor_EditValue_AddButton_CommitsColumnStyle()
    {
        using TableLayoutPanel panel = new();
        StyleCollectionEditor editor = new(panel.ColumnStyles.GetType());
        TestTypeDescriptorContext context = new(panel);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(panel.ColumnStyles, editor.EditValue(context, editorService, panel.ColumnStyles));
        Assert.Single(panel.ColumnStyles);
    }

    [UIFact]
    public void ToolStripCollectionEditor_EditValue_AddButton_CommitsToolStripItem()
    {
        using ToolStrip toolStrip = new();
        ToolStripCollectionEditor editor = new(toolStrip.Items.GetType());
        TestTypeDescriptorContext context = new(toolStrip);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(toolStrip.Items, editor.EditValue(context, editorService, toolStrip.Items));
        ToolStripItem item = Assert.IsAssignableFrom<ToolStripItem>(Assert.Single(toolStrip.Items));
        Assert.IsType<ToolStripButton>(item);
    }

    [UIFact]
    public void ToolStripCollectionEditor_EditValue_AddButton_CommitsDropDownItem()
    {
        using ToolStripDropDownButton ownerItem = new();
        ToolStripCollectionEditor editor = new(ownerItem.DropDownItems.GetType());
        TestTypeDescriptorContext context = new(ownerItem);
        ModalEditorService editorService = CreateAddAndOkCollectionEditorService();

        Assert.Same(ownerItem.DropDownItems, editor.EditValue(context, editorService, ownerItem.DropDownItems));
        ToolStripItem item = Assert.IsAssignableFrom<ToolStripItem>(Assert.Single(ownerItem.DropDownItems));
        Assert.IsType<ToolStripButton>(item);
    }

    [UIFact]
    public void ToolStripDropDownItemDesigner_Initialize_AddsEditItemsVerb()
    {
        using ToolStripDropDownButton ownerItem = new();
        using ToolStripDropDownItemDesigner designer = new();

        designer.Initialize(ownerItem);

        Assert.Contains(designer.Verbs.Cast<DesignerVerb>(), verb => verb.Text == "&Edit Items...");
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

    private static DataGridViewAddColumnDialog CreateDataGridViewAddColumnDialog(DataGridView liveDataGridView)
    {
        DataGridViewAddColumnDialog dialog = new(liveDataGridView.Columns, liveDataGridView);
        dialog.Start(liveDataGridView.Columns.Count, persistChangesToDesigner: false);
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

    private static ModalEditorService CreateAddAndOkCollectionEditorService()
        => new(dialog =>
        {
            Button addButton = GetPrivateField<Button>(dialog, "_addButton");
            Button okButton = GetPrivateField<Button>(dialog, "_okButton");

            addButton.PerformClick();
            okButton.PerformClick();
        });

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

    private sealed class ModalEditorService(Action<Form> onShown) : IServiceProvider, IWindowsFormsEditorService
    {
        public void CloseDropDown()
        {
        }

        public void DropDownControl(Control control)
        {
        }

        public object? GetService(Type serviceType)
            => serviceType == typeof(IWindowsFormsEditorService) ? this : null;

        public DialogResult ShowDialog(Form dialog)
        {
            dialog.Shown += (sender, e) => onShown(dialog);
            return dialog.ShowDialog();
        }
    }

    private sealed class TestTypeDescriptorContext(object instance) : ITypeDescriptorContext
    {
        public IContainer? Container => null;

        public object Instance { get; } = instance;

        public PropertyDescriptor? PropertyDescriptor => null;

        public void OnComponentChanged()
        {
        }

        public bool OnComponentChanging() => true;

        public object? GetService(Type serviceType) => null;
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
