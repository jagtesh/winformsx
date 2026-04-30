// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms;

public abstract partial class UpDownBase
{
    internal sealed class UpDownBaseAccessibleObject : ControlAccessibleObject
    {
        public UpDownBaseAccessibleObject(UpDownBase owner) : base(owner)
        { }

        public override AccessibleObject? GetChild(int index)
        {
            if (!this.TryGetOwnerAs(out UpDownBase? owner))
            {
                return null;
            }

            return index switch
            {
                // TextBox child
                0 => GetAccessibleChild(owner.TextBox.AccessibilityObject),
                // Up/down buttons
                1 => GetAccessibleChild(owner.UpDownButtonsInternal.AccessibilityObject),
                _ => null,
            };
        }

        public override AccessibleObject? GetFocused()
        {
            if (!this.TryGetOwnerAs(out UpDownBase? owner))
            {
                return null;
            }

            if (owner.TextBox.Focused)
            {
                return GetAccessibleChild(owner.TextBox.AccessibilityObject);
            }

            if (owner.UpDownButtonsInternal.Focused)
            {
                return GetAccessibleChild(owner.UpDownButtonsInternal.AccessibilityObject);
            }

            return owner.Focused ? this : null;
        }

        private static AccessibleObject? GetAccessibleChild(AccessibleObject accessibleObject)
        {
            AccessibleObject? parent = accessibleObject.Parent;
            return parent is not null && !ReferenceEquals(parent, accessibleObject)
                ? parent
                : accessibleObject;
        }

        private protected override bool IsInternal => true;

        public override int GetChildCount() => 2;

        public override AccessibleRole Role => this.GetOwnerAccessibleRole(AccessibleRole.SpinButton);
    }
}
