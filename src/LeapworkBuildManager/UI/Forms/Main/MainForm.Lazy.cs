using System;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        public bool AdvancedControlsCreated
        {
            get
            {
                return type != null;
            }
        }

        void EnsureAdvancedControls()
        {
            if (type != null)
                return;
            typeLabel = Caption("Build type");
            typeLabel.Font = Font;
            type = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Input,
                ForeColor = ForeColor,
                DrawMode = DrawMode.OwnerDrawFixed,
                Font = Font,
                ItemHeight = Px(23),
                AccessibleName = "Build type",
                TabIndex = 3
            };
            foreach (BuildKind kind in Enum.GetValues(typeof(BuildKind)))
                type.Items.Add(BuildKinds.Display(kind));
            type.SelectedItem = BuildKinds.Display(selectedBuildType);
            type.DrawItem += DrawCombo;
            Place(details, typeLabel, Px(16), Px(140), Px(90), Px(24));
            Place(details, type, Px(110), Px(136), Px(446), Px(28));
            type.Visible = typeLabel.Visible = advancedMode;
            type.SelectedIndexChanged += delegate
            {
                selectedBuildType = BuildKinds.Parse(type.Text);
                Changed();
            };
        }
    }
}
