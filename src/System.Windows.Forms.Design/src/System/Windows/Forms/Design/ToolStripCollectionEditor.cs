// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Design;

namespace System.Windows.Forms.Design;

internal class ToolStripCollectionEditor : CollectionEditor
{
    public ToolStripCollectionEditor(Type type)
        : base(type)
    {
    }

    protected override Type[] CreateNewItemTypes()
        =>
        [
            typeof(ToolStripButton),
            typeof(ToolStripLabel),
            typeof(ToolStripSplitButton),
            typeof(ToolStripDropDownButton),
            typeof(ToolStripSeparator),
            typeof(ToolStripComboBox),
            typeof(ToolStripTextBox),
            typeof(ToolStripProgressBar)
        ];

    protected override string GetDisplayText(object? value)
    {
        if (value is ToolStripItem item)
        {
            if (!string.IsNullOrEmpty(item.Name))
            {
                return item.Name;
            }

            if (!string.IsNullOrEmpty(item.Text))
            {
                return item.Text;
            }
        }

        return base.GetDisplayText(value);
    }
}
