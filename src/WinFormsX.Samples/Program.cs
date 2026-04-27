// WinFormsX Kitchen Sink Demo
// Showcases every control implemented in Phase 1 and Phase 2.

using System.Drawing;
using System.Windows.Forms;

namespace WinFormsX.Samples;

public static class Program
{
    [STAThread]
    public static async Task Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new KitchenSinkForm());
#if BROWSER
        // WASM: Keep the runtime alive so JSExport callbacks work.
        // The JS requestAnimationFrame loop drives rendering and input.
        await Task.Delay(-1);
#else
        await Task.CompletedTask;
#endif
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Kitchen Sink — exercises every control in the framework
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

public class KitchenSinkForm : Form
{
    // Status bar for event feedback
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;

    public KitchenSinkForm()
    {
        Text = "WinFormsX — Kitchen Sink Demo";
        Size = new Size(900, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = SystemColors.Control;
        
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(7F, 15F);

        SuspendLayout();

        // ─── Menu Bar (Dock.Top) ────────────────────────────────
        var menuStrip = BuildMenuStrip();
        menuStrip.Dock = DockStyle.Top;

        // ─── Status Bar (Dock.Bottom) ───────────────────────────
        _statusLabel = new ToolStripStatusLabel("Ready");
        _statusStrip = new StatusStrip();
        _statusStrip.Dock = DockStyle.Bottom;
        _statusStrip!.Items.Add(_statusLabel);
        _statusStrip!.Items.Add(new ToolStripStatusLabel("WinFormsX v0.1.0-alpha.1"));

        // ─── Main Content — TabControl (Dock.Fill) ──────────────
        var tabControl = new TabControl { Dock = DockStyle.Fill };

        // Add a rendering test tab first so it's visible by default
        tabControl.TabPages.Add(BuildRenderingTestTab());
        tabControl.TabPages.Add(BuildBasicControlsTab());
        tabControl.TabPages.Add(BuildListsAndCombosTab());
        tabControl.TabPages.Add(BuildLayoutTab());
        tabControl.TabPages.Add(BuildDialogsTab());
        tabControl.TabPages.Add(BuildDrawingTab());
        tabControl.TabPages.Add(BuildDataTab());

        // Add TabControl first (Index 0 => Top of Z-Order => Evaluated LAST in layout)
        Controls.Add(tabControl);
        Controls.Add(menuStrip);
        Controls.Add(_statusStrip);
        MainMenuStrip = menuStrip;

        ResumeLayout(true);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Rendering Test Tab
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildRenderingTestTab()
    {
        var page = new TabPage("Rendering Test");

        var lblTitle = new Label { Text = "WinFormsX Rendering Test", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(0, 120, 215), Location = new Point(20, 10), AutoSize = true };
        var lblSub = new Label { Text = "Controls rendered via Impeller GPU backend", Font = new Font("Segoe UI", 10), ForeColor = Color.Gray, Location = new Point(20, 45), AutoSize = true };

        var btnPrimary = new Button { Text = "Primary", Size = new Size(140, 40), Location = new Point(20, 90), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnSuccess = new Button { Text = "Success", Size = new Size(140, 40), Location = new Point(180, 90), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnDanger = new Button { Text = "Danger", Size = new Size(140, 40), Location = new Point(340, 90), BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnWarning = new Button { Text = "Warning", Size = new Size(140, 40), Location = new Point(500, 90), BackColor = Color.FromArgb(255, 193, 7), ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };

        var darkPanel = new Panel { Location = new Point(20, 160), Size = new Size(620, 80), BackColor = Color.FromArgb(24, 24, 32) };
        darkPanel.Controls.Add(new Label { Text = "Dark Panel — Custom BackColor", ForeColor = Color.FromArgb(0, 188, 212), Font = new Font("Segoe UI", 12), Location = new Point(10, 25), AutoSize = true });

        var orangePanel = new Panel { Location = new Point(20, 260), Size = new Size(300, 60), BackColor = Color.FromArgb(255, 152, 0) };
        orangePanel.Controls.Add(new Label { Text = "Orange", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(10, 18), AutoSize = true });

        var greenPanel = new Panel { Location = new Point(340, 260), Size = new Size(300, 60), BackColor = Color.FromArgb(76, 175, 80) };
        greenPanel.Controls.Add(new Label { Text = "Green", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(10, 18), AutoSize = true });

        var magentaPanel = new Panel { Location = new Point(20, 340), Size = new Size(300, 60), BackColor = Color.FromArgb(171, 71, 188) };
        magentaPanel.Controls.Add(new Label { Text = "Magenta", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(10, 18), AutoSize = true });

        var cyanPanel = new Panel { Location = new Point(340, 340), Size = new Size(300, 60), BackColor = Color.FromArgb(0, 188, 212) };
        cyanPanel.Controls.Add(new Label { Text = "Cyan", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(10, 18), AutoSize = true });

        page.Controls.AddRange(new Control[] { lblTitle, lblSub, btnPrimary, btnSuccess, btnDanger, btnWarning, darkPanel, orangePanel, greenPanel, magentaPanel, cyanPanel });
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 1 — Basic Controls
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━


    private TabPage BuildBasicControlsTab()
    {
        var page = new TabPage("Basic Controls");
        
        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // ── Left Side (Inputs & Buttons) ──
        var leftFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        
        var lblTitle = new Label { Text = "Labels and Text", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 16) };
        var lblNormal = new Label { Text = "This is a standard Label control.", AutoSize = true, Margin = new Padding(0, 0, 0, 20) };
        
        var lblInput = new Label { Text = "Your Name:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        var txtInput = new TextBox { Text = "Ada Lovelace", Width = 220, Height = 25, Margin = new Padding(0, 0, 0, 16) };
        txtInput.TextChanged += (_, _) => SetStatus($"TextBox.TextChanged: \"{txtInput.Text}\"");

        var lblPassword = new Label { Text = "Password:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        var txtPassword = new TextBox { Text = "secret", Width = 220, Height = 25, PasswordChar = '●', Margin = new Padding(0, 0, 0, 20) };

        var grpButtons = new GroupBox { Text = "Buttons", Size = new Size(320, 150), Margin = new Padding(0, 0, 0, 16) };
        var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var btnNormal = new Button { Text = "Normal", AutoSize = true };
        btnNormal.Click += (_, _) => SetStatus("Normal button clicked!");
        var btnFlat = new Button { Text = "Flat", AutoSize = true, FlatStyle = FlatStyle.Flat };
        btnFlat.Click += (_, _) => SetStatus("Flat button clicked!");
        var btnDisabled = new Button { Text = "Disabled", AutoSize = true, Enabled = false };
        var btnAccent = new Button { Text = "Accent", AutoSize = true };
        btnAccent.Click += (_, _) => SetStatus($"Hello, {txtInput.Text}! Welcome to WinFormsX.");
        btnFlow.Controls.AddRange(btnNormal, btnFlat, btnDisabled, btnAccent);
        grpButtons.Controls.Add(btnFlow);

        leftFlow.Controls.AddRange(lblTitle, lblNormal, lblInput, txtInput, lblPassword, txtPassword, grpButtons);
        mainLayout.Controls.Add(leftFlow);

        // ── Right Side (Checks & Visuals) ──
        var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        
        var grpChecks = new GroupBox { Text = "CheckBox & RadioButton", Size = new Size(340, 160), Margin = new Padding(0, 0, 0, 20) };
        var checkTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8) };
        checkTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); checkTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        checkTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
        checkTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
        checkTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
        
        var chk1 = new CheckBox { Text = "Option A", AutoSize = true, Checked = true };
        chk1.CheckedChanged += (_, _) => SetStatus($"CheckBox A: {(chk1.Checked ? "Checked" : "Unchecked")}");
        var chk2 = new CheckBox { Text = "Option B", AutoSize = true };
        var chk3 = new CheckBox { Text = "Three-State", AutoSize = true, ThreeState = true };
        chk3.CheckedChanged += (_, _) => SetStatus($"Three-state: {chk3.CheckState}");
        
        var rb1 = new RadioButton { Text = "Red", AutoSize = true, Checked = true };
        var rb2 = new RadioButton { Text = "Green", AutoSize = true };
        var rb3 = new RadioButton { Text = "Blue", AutoSize = true };
        rb1.CheckedChanged += (_, _) => { if (rb1.Checked) SetStatus("Color: Red"); };
        rb2.CheckedChanged += (_, _) => { if (rb2.Checked) SetStatus("Color: Green"); };
        rb3.CheckedChanged += (_, _) => { if (rb3.Checked) SetStatus("Color: Blue"); };
        
        checkTable.Controls.AddRange(chk1, chk2, chk3, rb1, rb2, rb3);
        grpChecks.Controls.Add(checkTable);

        var grpPanels = new GroupBox { Text = "Panel Border Styles", Size = new Size(320, 120), Margin = new Padding(0, 0, 0, 20) };
        var panelFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var panelNone = new Panel { BorderStyle = BorderStyle.None, Size = new Size(80, 55), BackColor = SystemColors.ControlLight, Margin = new Padding(4) };
        panelNone.Controls.Add(new Label { Text = "None", AutoSize = true, Location = new Point(4, 4) });
        var panelSingle = new Panel { BorderStyle = BorderStyle.FixedSingle, Size = new Size(80, 55), BackColor = SystemColors.ControlLightLight, Margin = new Padding(4) };
        panelSingle.Controls.Add(new Label { Text = "Single", AutoSize = true, Location = new Point(4, 4) });
        var panel3D = new Panel { BorderStyle = BorderStyle.Fixed3D, Size = new Size(80, 55), BackColor = SystemColors.ButtonFace, Margin = new Padding(4) };
        panel3D.Controls.Add(new Label { Text = "3D", AutoSize = true, Location = new Point(4, 4) });
        panelFlow.Controls.AddRange(panelNone, panelSingle, panel3D);
        grpPanels.Controls.Add(panelFlow);

        var picBox = new PictureBox { Size = new Size(240, 90), BackColor = SystemColors.ControlLightLight, Margin = new Padding(0, 0, 0, 20) };
        picBox.Controls.Add(new Label { Text = "PictureBox\n(Phase 3)", Location = new Point(8, 8), AutoSize = true, ForeColor = SystemColors.ControlDark });
        
        var lblEventsTitle = new Label { Text = "Event Log:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        var eventPanel = new Panel { Size = new Size(320, 80), BorderStyle = BorderStyle.Fixed3D, BackColor = SystemColors.ControlDarkDark };
        var eventLabel = new Label { Text = "> Ready.\n> Interact with controls.", Size = new Size(300, 70), ForeColor = Color.FromArgb(0, 255, 100), Location = new Point(4,4) };
        eventPanel.Controls.Add(eventLabel);
        
        _statusStrip.Items![0].Click += (_, _) => { eventLabel.Text = $"> {_statusLabel.Text}\n{eventLabel.Text}"; if (eventLabel.Text.Length > 200) eventLabel.Text = eventLabel.Text[..200]; };
        
        rightFlow.Controls.AddRange(grpChecks, grpPanels, picBox, lblEventsTitle, eventPanel);
        mainLayout.Controls.Add(rightFlow);

        page.Controls.Add(mainLayout);
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 2 — Lists & Combos
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildListsAndCombosTab()
    {
        var page = new TabPage("Lists & Combos");

        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

        // ── Column 1: ComboBoxes & Available List ──
        var col1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        
        var lblCombo = new Label { Text = "ComboBox", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 12) };
        
        var flowFruit = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        var lblFruit = new Label { Text = "Fruit:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
        var cboFruit = new ComboBox { Width = 180 };
        cboFruit.Items.AddRange(["Apple", "Banana", "Cherry", "Dragon Fruit", "Elderberry", "Fig", "Grape", "Honeydew", "Kiwi", "Lemon", "Mango"]);
        cboFruit.SelectedIndex = 0;
        cboFruit.SelectedIndexChanged += (_, _) => SetStatus($"ComboBox: Selected \"{cboFruit.SelectedItem}\"");
        flowFruit.Controls.AddRange(lblFruit, cboFruit);

        var flowCountry = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 24) };
        var lblCountry = new Label { Text = "Country:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
        var cboCountry = new ComboBox { Width = 180, Sorted = true };
        cboCountry.Items.AddRange(["United States", "Japan", "Germany", "Brazil", "India", "Australia", "Canada", "United Kingdom", "France", "South Korea"]);
        cboCountry.SelectedIndex = 0;
        cboCountry.SelectedIndexChanged += (_, _) => SetStatus($"Country: {cboCountry.SelectedItem}");
        flowCountry.Controls.AddRange(lblCountry, cboCountry);

        var lblAvailable = new Label { Text = "Available Employees:", AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 8) };
        var lstAvailable = new ListBox { Width = 220, Height = 250, BorderStyle = BorderStyle.Fixed3D };
        lstAvailable.Items.AddRange(["Alice Smith", "Bob Jones", "Charlie Davis", "Diana Prince", "Evan Wright", "Fiona Gallagher", "George Clark", "Hannah Lee", "Ian Malcolm", "Julia Roberts"]);
        lstAvailable.SelectedIndex = 0;
        lstAvailable.SelectedIndexChanged += (_, _) => SetStatus($"Available Selected: {lstAvailable.SelectedItem}");

        col1.Controls.AddRange(lblCombo, flowFruit, flowCountry, lblAvailable, lstAvailable);
        mainLayout.Controls.Add(col1);

        // ── Column 2: Assigned Team ──
        var col2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 125, 16, 16) };
        
        var lblAssigned = new Label { Text = "Project Team:", AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 8) };
        var lstAssigned = new ListBox { Width = 200, Height = 250, BorderStyle = BorderStyle.FixedSingle, Sorted = true };
        lstAssigned.SelectedIndexChanged += (_, _) => SetStatus($"Team Member: {lstAssigned.SelectedItem}");

        col2.Controls.AddRange(lblAssigned, lstAssigned);
        mainLayout.Controls.Add(col2);

        // ── Column 3: Team Management ──
        var col3 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 125, 16, 16) };
        
        var grpManage = new GroupBox { Text = "Team Management", Width = 260, Height = 250 };
        var manageFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };

        var lblCount = new Label { Text = "Count: Available: 10  Team: 0", AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        var btnAssign = new Button { Text = "Assign →", Width = 200, Height = 28, Margin = new Padding(0, 0, 0, 8) };
        var btnUnassign = new Button { Text = "← Unassign", Width = 200, Height = 28, Margin = new Padding(0, 0, 0, 16) };
        
        var flowHire = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        var txtNewEmp = new TextBox { Width = 120, Text = "New Hire" };
        var btnAddEmp = new Button { Text = "Hire", Width = 70 };
        flowHire.Controls.AddRange(txtNewEmp, btnAddEmp);

        var btnClearAll = new Button { Text = "Clear Project", Width = 200, Height = 28 };

        Action updateCount = () => lblCount.Text = $"Count: Available: {lstAvailable.Items.Count}  Team: {lstAssigned.Items.Count}";

        btnAssign.Click += (_, _) => {
            if (lstAvailable.SelectedItem != null) {
                var employee = lstAvailable.SelectedItem.ToString()!;
                lstAvailable.Items.Remove(lstAvailable.SelectedItem);
                lstAssigned.Items.Add(employee);
                SetStatus($"Assigned: {employee}");
                updateCount();
            }
        };
        btnUnassign.Click += (_, _) => {
            if (lstAssigned.SelectedItem != null) {
                var employee = lstAssigned.SelectedItem.ToString()!;
                lstAssigned.Items.Remove(lstAssigned.SelectedItem);
                lstAvailable.Items.Add(employee);
                SetStatus($"Unassigned: {employee}");
                updateCount();
            }
        };
        btnAddEmp.Click += (_, _) => {
            if (!string.IsNullOrWhiteSpace(txtNewEmp.Text)) {
                lstAvailable.Items.Add(txtNewEmp.Text);
                SetStatus($"Hired: {txtNewEmp.Text}");
                txtNewEmp.Text = string.Empty;
                updateCount();
            }
        };
        btnClearAll.Click += (_, _) => {
            for (int i = 0; i < lstAssigned.Items.Count; i++) lstAvailable.Items.Add(lstAssigned.Items[i]);
            lstAssigned.Items.Clear();
            SetStatus("Project team cleared");
            updateCount();
        };

        manageFlow.Controls.AddRange(lblCount, btnAssign, btnUnassign, flowHire, btnClearAll);
        grpManage.Controls.Add(manageFlow);

        col3.Controls.Add(grpManage);
        mainLayout.Controls.Add(col3);

        page.Controls.Add(mainLayout);
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 3 — Layout Panels
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildLayoutTab()
    {
        var page = new TabPage("Layout");

        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // ── Quadrant 1: SplitContainer ──
        var quad1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblSplit = new Label { Text = "SplitContainer", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 4) };
        var lblSplitDesc = new Label { Text = "Drag the divider to resize panels:", AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        
        var split = new SplitContainer { Width = 380, Height = 200, SplitterDistance = 180, Orientation = Orientation.Vertical };
        split.Panel1.BackColor = SystemColors.ControlLight;
        split.Panel2.BackColor = SystemColors.ControlLightLight;
        var lblP1 = new Label { Text = "Panel 1", Location = new Point(8, 8), Size = new Size(100, 20), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        var lblP2 = new Label { Text = "Panel 2", Location = new Point(8, 8), Size = new Size(100, 20), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        split.Panel1.Controls.Add(lblP1);
        split.Panel2.Controls.Add(lblP2);
        
        quad1.Controls.AddRange(lblSplit, lblSplitDesc, split);
        mainLayout.Controls.Add(quad1);

        // ── Quadrant 2: FlowLayoutPanel ──
        var quad2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblFlow = new Label { Text = "FlowLayoutPanel", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 4) };
        var lblFlowDesc = new Label { Text = "Auto-wrapping flow of controls:", AutoSize = true, Margin = new Padding(0, 0, 0, 12) };

        var flowPanel = new FlowLayoutPanel { Width = 380, Height = 200, BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window, WrapContents = true, Padding = new Padding(8) };
        var tagColors = new[] { Color.FromArgb(239, 83, 80), Color.FromArgb(102, 187, 106), Color.FromArgb(66, 165, 245), Color.FromArgb(255, 167, 38), Color.FromArgb(171, 71, 188), Color.FromArgb(38, 198, 218), Color.FromArgb(255, 238, 88), Color.FromArgb(141, 110, 99) };
        var tagNames = new[] { "C#", "Impeller", "HarfBuzz", "Win32", "Vulkan", "Metal", "OpenGL", "NuGet" };
        for (int i = 0; i < tagNames.Length; i++)
        {
            var tag = new Button { Text = tagNames[i], AutoSize = true, BackColor = tagColors[i], ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(4) };
            tag.Click += (s, _) => SetStatus($"Tag: {((Button)s!).Text}");
            flowPanel.Controls.Add(tag);
        }
        
        quad2.Controls.AddRange(lblFlow, lblFlowDesc, flowPanel);
        mainLayout.Controls.Add(quad2);

        // ── Quadrant 3: TableLayoutPanel ──
        var quad3 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblTable = new Label { Text = "TableLayoutPanel", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 16) };

        var tablePanel = new TableLayoutPanel { Width = 380, Height = 200, ColumnCount = 3, RowCount = 3 };
        tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        tablePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tablePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tablePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var hdr1 = new Label { Text = "Name", BackColor = Color.FromArgb(50, 50, 70), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var hdr2 = new Label { Text = "Type", BackColor = Color.FromArgb(50, 50, 70), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var hdr3 = new Label { Text = "Phase", BackColor = Color.FromArgb(50, 50, 70), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var cell1 = new Label { Text = "ComboBox", BackColor = Color.FromArgb(240, 240, 250), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        var cell2 = new Label { Text = "Composite", BackColor = Color.FromArgb(240, 240, 250), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var cell3 = new Label { Text = "Phase 2", BackColor = Color.FromArgb(200, 255, 200), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var cell4 = new Label { Text = "DataGridView", BackColor = Color.FromArgb(245, 245, 255), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        var cell5 = new Label { Text = "Data", BackColor = Color.FromArgb(245, 245, 255), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        var cell6 = new Label { Text = "Phase 3", BackColor = Color.FromArgb(255, 245, 200), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };

        tablePanel.Controls.AddRange(hdr1, hdr2, hdr3, cell1, cell2, cell3, cell4, cell5, cell6);
        
        quad3.Controls.AddRange(lblTable, tablePanel);
        mainLayout.Controls.Add(quad3);

        // ── Quadrant 4: Docking ──
        var quad4 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var grpDock = new GroupBox { Text = "Dock Layout", Width = 380, Height = 230 };
        var dockTop = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = SystemColors.ControlDark };
        dockTop.Controls.Add(new Label { Text = "Top", ForeColor = Color.White, Location = new Point(4, 6), Size = new Size(40, 16) });
        var dockBottom = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = SystemColors.ControlDark };
        dockBottom.Controls.Add(new Label { Text = "Bottom", ForeColor = Color.White, Location = new Point(4, 6), Size = new Size(50, 16) });
        var dockLeft = new Panel { Dock = DockStyle.Left, Width = 50, BackColor = SystemColors.ControlLight };
        dockLeft.Controls.Add(new Label { Text = "L", ForeColor = SystemColors.ControlText, Location = new Point(4, 20), Size = new Size(20, 16) });
        var dockRight = new Panel { Dock = DockStyle.Right, Width = 50, BackColor = SystemColors.ControlLight };
        dockRight.Controls.Add(new Label { Text = "R", ForeColor = SystemColors.ControlText, Location = new Point(4, 20), Size = new Size(20, 16) });
        var dockFill = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window };
        dockFill.Controls.Add(new Label { Text = "Fill", ForeColor = SystemColors.ControlText, Location = new Point(20, 20), Size = new Size(30, 16) });

        grpDock.Controls.Add(dockFill);
        grpDock.Controls.Add(dockLeft);
        grpDock.Controls.Add(dockRight);
        grpDock.Controls.Add(dockTop);
        grpDock.Controls.Add(dockBottom);

        quad4.Controls.Add(grpDock);
        mainLayout.Controls.Add(quad4);

        page.Controls.Add(mainLayout);
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 4 — Dialogs
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildDialogsTab()
    {
        var page = new TabPage("Dialogs");

        var mainFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };

        var lblTitle = new Label { Text = "Common Dialogs", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 4) };
        var lblDesc = new Label { Text = "These dialogs delegate to the native OS dialog (comdlg32.dll on Windows).", AutoSize = true, Margin = new Padding(0, 0, 0, 16) };

        var buttonFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 16) };

        var resultPanel = new Panel { Width = 820, Height = 150, BorderStyle = BorderStyle.Fixed3D, BackColor = Color.FromArgb(30, 30, 30), Margin = new Padding(0, 16, 0, 0) };
        var resultLabel = new Label { Text = "> Click a button above to open a dialog.\n> The result will appear here.", Size = new Size(800, 130), ForeColor = Color.FromArgb(0, 255, 100), Location = new Point(8, 8) };
        resultPanel.Controls.Add(resultLabel);

        // ── OpenFileDialog ──
        var btnOpen = new Button { Text = "📂  Open File...", Size = new Size(180, 40), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, Margin = new Padding(0, 0, 16, 0) };
        btnOpen.Click += (_, _) => {
            var dlg = new OpenFileDialog { Title = "Open a File", Filter = "Text Files|*.txt|C# Files|*.cs|All Files|*.*", FilterIndex = 1 };
            var result = dlg.ShowDialog();
            resultLabel.Text = result == DialogResult.OK ? $"> OpenFileDialog: OK\n> File: {dlg.FileName}" : "> OpenFileDialog: Cancelled";
            SetStatus(resultLabel.Text.Replace("\n", " | "));
        };

        // ── SaveFileDialog ──
        var btnSave = new Button { Text = "💾  Save File...", Size = new Size(180, 40), BackColor = Color.FromArgb(56, 142, 60), ForeColor = Color.White, Margin = new Padding(0, 0, 16, 0) };
        btnSave.Click += (_, _) => {
            var dlg = new SaveFileDialog { Title = "Save a File", Filter = "Text Files|*.txt|All Files|*.*", FileName = "untitled.txt" };
            var result = dlg.ShowDialog();
            resultLabel.Text = result == DialogResult.OK ? $"> SaveFileDialog: OK\n> File: {dlg.FileName}" : "> SaveFileDialog: Cancelled";
            SetStatus(resultLabel.Text.Replace("\n", " | "));
        };

        // ── ColorDialog ──
        var btnColor = new Button { Text = "🎨  Pick Color...", Size = new Size(180, 40), BackColor = Color.FromArgb(171, 71, 188), ForeColor = Color.White, Margin = new Padding(0, 0, 16, 0) };
        var colorPreview = new Panel { Size = new Size(160, 40), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(100, 149, 237), Margin = new Padding(0, 0, 16, 0) };
        var colorLabel = new Label { Text = "CornflowerBlue\n#6495ED", Size = new Size(150, 36), ForeColor = Color.White, Location = new Point(4, 2) };
        colorPreview.Controls.Add(colorLabel);

        btnColor.Click += (_, _) => {
            var dlg = new ColorDialog { Color = colorPreview.BackColor, FullOpen = true };
            var result = dlg.ShowDialog();
            if (result == DialogResult.OK) {
                colorPreview.BackColor = dlg.Color;
                colorLabel.Text = $"R:{dlg.Color.R} G:{dlg.Color.G} B:{dlg.Color.B}";
                resultLabel.Text = $"> ColorDialog: OK\n> Color: RGB({dlg.Color.R}, {dlg.Color.G}, {dlg.Color.B})";
                SetStatus($"Color: RGB({dlg.Color.R},{dlg.Color.G},{dlg.Color.B})");
            } else { resultLabel.Text = "> ColorDialog: Cancelled"; }
        };

        // ── FontDialog ──
        var btnFont = new Button { Text = "🔤  Font...", Size = new Size(180, 40), BackColor = Color.FromArgb(255, 167, 38), ForeColor = Color.White };
        btnFont.Click += (_, _) => {
            var dlg = new FontDialog();
            var result = dlg.ShowDialog();
            resultLabel.Text = result == DialogResult.OK ? $"> FontDialog: OK\n> Font: {dlg.Font}" : "> FontDialog: Not yet implemented (Phase 3)";
            SetStatus("FontDialog opened (stub — full CHOOSEFONTW in Phase 3)");
        };

        var colorFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 16, 0) };
        colorFlow.Controls.AddRange(btnColor, colorPreview);
        
        buttonFlow.Controls.AddRange(btnOpen, btnSave, btnColor, colorPreview, btnFont);
        
        mainFlow.Controls.AddRange(lblTitle, lblDesc, buttonFlow, resultPanel);

        page.Controls.Add(mainFlow);
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 5 — Drawing Canvas
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildDrawingTab()
    {
        var page = new TabPage("Drawing");
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };

        var lblTitle = new Label { Text = "Graphics Drawing Primitives", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 4) };
        var lblDesc = new Label { Text = "The canvas below is rendered entirely through the Impeller DisplayListBuilder.", AutoSize = true, Margin = new Padding(0, 0, 0, 16) };

        var canvas = new DrawingCanvas { Width = 830, Height = 400, BackColor = Color.FromArgb(24, 24, 32) };

        layout.Controls.AddRange(lblTitle, lblDesc, canvas);
        page.Controls.Add(layout);
        return page;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Menu Bar
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private MenuStrip BuildMenuStrip()
    {
        var menuStrip = new MenuStrip();

        // File
        var mnuFile = new ToolStripMenuItem("File");
        mnuFile.DropDownItems.Add(new ToolStripMenuItem("New", null, (_, _) => SetStatus("File > New")));
        mnuFile.DropDownItems.Add(new ToolStripMenuItem("Open...", null, (_, _) => SetStatus("File > Open")));
        mnuFile.DropDownItems.Add(new ToolStripMenuItem("Save", null, (_, _) => SetStatus("File > Save")) { ShortcutKeyDisplayString = "Ctrl+S" });
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (_, _) => Close()));

        // Edit
        var mnuEdit = new ToolStripMenuItem("Edit");
        mnuEdit.DropDownItems.Add(new ToolStripMenuItem("Undo", null, (_, _) => SetStatus("Edit > Undo")) { ShortcutKeyDisplayString = "Ctrl+Z" });
        mnuEdit.DropDownItems.Add(new ToolStripMenuItem("Redo", null, (_, _) => SetStatus("Edit > Redo")) { ShortcutKeyDisplayString = "Ctrl+Y" });
        mnuEdit.DropDownItems.Add(new ToolStripSeparator());
        mnuEdit.DropDownItems.Add(new ToolStripMenuItem("Cut", null, (_, _) => SetStatus("Edit > Cut")) { ShortcutKeyDisplayString = "Ctrl+X" });
        mnuEdit.DropDownItems.Add(new ToolStripMenuItem("Copy", null, (_, _) => SetStatus("Edit > Copy")) { ShortcutKeyDisplayString = "Ctrl+C" });
        mnuEdit.DropDownItems.Add(new ToolStripMenuItem("Paste", null, (_, _) => SetStatus("Edit > Paste")) { ShortcutKeyDisplayString = "Ctrl+V" });

        // View
        var mnuView = new ToolStripMenuItem("View");
        mnuView.DropDownItems.Add(new ToolStripMenuItem("Status Bar", null, (_, _) => SetStatus("View > Status Bar")));
        mnuView.DropDownItems.Add(new ToolStripMenuItem("Full Screen", null, (_, _) => SetStatus("View > Full Screen")) { ShortcutKeyDisplayString = "F11" });

        // Help
        var mnuHelp = new ToolStripMenuItem("Help");
        mnuHelp.DropDownItems.Add(new ToolStripMenuItem("About WinFormsX", null, (_, _) =>
            SetStatus("WinFormsX v0.1.0-alpha.1 — Cross-platform WinForms on Impeller")));

        menuStrip.Items.Add(mnuFile);
        menuStrip.Items.Add(mnuEdit);
        menuStrip.Items.Add(mnuView);
        menuStrip.Items.Add(mnuHelp);

        return menuStrip;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Tab 6 — Data (TreeView + ListView)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private TabPage BuildDataTab()
    {
        var page = new TabPage("Data");

        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        var topSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 300 };

        // ── TreeView ──
        var col1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblTree = new Label { Text = "TreeView", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 8) };
        var tree = new TreeView { Width = 280, Height = 250, CheckBoxes = true, ShowPlusMinus = true, ShowLines = true };

        var root = new TreeNode("C:\\");
        var programFiles = new TreeNode("Program Files", new[] { new TreeNode("Microsoft Office", new[] { new TreeNode("winword.exe"), new TreeNode("excel.exe"), new TreeNode("powerpnt.exe") }), new TreeNode("Visual Studio", new[] { new TreeNode("devenv.exe"), new TreeNode("MSBuild.dll") }), new TreeNode("Git", new[] { new TreeNode("git.exe"), new TreeNode("bash.exe") }) });
        var users = new TreeNode("Users", new[] { new TreeNode("Public", new[] { new TreeNode("Documents"), new TreeNode("Downloads") }), new TreeNode("Developer", new[] { new TreeNode("Desktop"), new TreeNode("Projects", new[] { new TreeNode("WinFormsX"), new TreeNode("MyApp") }) }) });
        var windows = new TreeNode("Windows", new[] { new TreeNode("System32", new[] { new TreeNode("cmd.exe"), new TreeNode("notepad.exe") }), new TreeNode("Fonts") });
        root.Nodes.AddRange(programFiles, users, windows);
        tree.Nodes.Add(root);
        root.Expand();

        tree.AfterSelect += (_, e) => SetStatus($"TreeView: Selected \"{e.Node?.Text}\" (Path: {e.Node?.FullPath})");
        tree.AfterCheck += (_, e) => SetStatus($"TreeView: {e.Node?.Text} Checked={e.Node?.Checked}");

        col1.Controls.AddRange(lblTree, tree);
        topSplit.Panel1.Controls.Add(col1);

        // ── ListView ──
        var col2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblList = new Label { Text = "ListView (Details)", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 8) };
        var listView = new ListView { Width = 560, Height = 250, View = View.Details, FullRowSelect = true, GridLines = true };

        listView.Columns.Add("Name", 180);
        listView.Columns.Add("Type", 100);
        listView.Columns.Add("Size", 80, HorizontalAlignment.Right);
        listView.Columns.Add("Modified", 120);

        listView.Items.AddRange(
            new ListViewItem(new[] { "Program.cs", "C# Source", "12 KB", "2026-04-10" }),
            new ListViewItem(new[] { "App.config", "XML Config", "2 KB", "2026-04-08" }),
            new ListViewItem(new[] { "README.md", "Markdown", "5 KB", "2026-04-11" }),
            new ListViewItem(new[] { "build.log", "Log File", "148 KB", "2026-04-11" }),
            new ListViewItem(new[] { "MainForm.cs", "C# Source", "8 KB", "2026-04-09" }),
            new ListViewItem(new[] { "Controls.dll", "Assembly", "256 KB", "2026-04-07" }),
            new ListViewItem(new[] { "icon.ico", "Icon", "1 KB", "2026-03-15" }),
            new ListViewItem(new[] { "styles.css", "Stylesheet", "4 KB", "2026-04-01" }),
            new ListViewItem(new[] { "database.db", "Database", "1.2 MB", "2026-04-10" }),
            new ListViewItem(new[] { "impeller.dll", "Native Lib", "6.2 MB", "2026-04-05" })
        );

        listView.SelectedIndexChanged += (_, _) => { var item = listView.FocusedItem; if (item != null) SetStatus($"ListView: Selected \"{item.Text}\""); };
        listView.ColumnClick += (_, e) => SetStatus($"ListView: Sorted by column {listView.Columns[e.Column].Text}");

        col2.Controls.AddRange(lblList, listView);
        topSplit.Panel2.Controls.Add(col2);

        // ── DataGridView ──
        var bottomFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var lblGrid = new Label { Text = "DataGridView", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 8) };
        var dgv = new DataGridView { Width = 840, Height = 200, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 245, 250) } };

        dgv.Columns.Add("ID", "ID"); dgv.Columns["ID"]!.Width = 50;
        dgv.Columns.Add("Name", "Employee Name"); dgv.Columns["Name"]!.Width = 180;
        dgv.Columns.Add("Dept", "Department"); dgv.Columns["Dept"]!.Width = 140;
        dgv.Columns.Add("Salary", "Salary"); dgv.Columns["Salary"]!.Width = 100;
        dgv.Columns.Add("Start", "Start Date"); dgv.Columns["Start"]!.Width = 120;

        dgv.Rows.Add("1", "Alice Smith", "Engineering", "$120,000", "2021-03-15");
        dgv.Rows.Add("2", "Bob Jones", "Marketing", "$95,000", "2019-07-01");
        dgv.Rows.Add("3", "Charlie Davis", "Engineering", "$110,000", "2020-11-20");
        dgv.Rows.Add("4", "Diana Prince", "HR", "$88,000", "2022-01-10");
        dgv.Rows.Add("5", "Evan Wright", "Engineering", "$105,000", "2023-06-05");
        dgv.Rows.Add("6", "Fiona Gallagher", "Finance", "$98,000", "2021-09-12");
        dgv.Rows.Add("7", "George Clark", "Marketing", "$92,000", "2020-04-30");
        dgv.Rows.Add("8", "Hannah Lee", "Engineering", "$115,000", "2022-08-22");

        dgv.SelectionChanged += (_, _) => { var row = dgv.CurrentRow; if (row != null && row.Cells.Count > 1) SetStatus($"DataGridView: Selected Row {row.Index + 1} — {row.Cells[1].FormattedValue}"); };
        var lblHint = new Label { Text = "Click column headers to sort. Use arrow keys to navigate rows.", AutoSize = true, ForeColor = SystemColors.ControlDark };

        bottomFlow.Controls.AddRange(lblGrid, dgv, lblHint);

        mainLayout.Controls.Add(topSplit);
        mainLayout.Controls.Add(bottomFlow);

        page.Controls.Add(mainLayout);
        return page;
    }

    private void SetStatus(string text) => _statusLabel.Text = text;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Custom Drawing Canvas — shows off Graphics API
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

public class DrawingCanvas : Control
{
    public DrawingCanvas() { BackColor = Color.FromArgb(24, 24, 32); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        g.ScaleTransform(DeviceDpi / 96f, DeviceDpi / 96f);

        // Background
        using var bgBrush = new SolidBrush(BackColor);
        g.FillRectangle(bgBrush, rect);

        // ── Grid ──
        using var gridPen = new Pen(Color.FromArgb(40, 40, 55));
        for (int x = 0; x < rect.Width; x += 20)
            g.DrawLine(gridPen, x, 0, x, rect.Height);
        for (int y = 0; y < rect.Height; y += 20)
            g.DrawLine(gridPen, 0, y, rect.Width, y);

        // ── Filled Rectangles ──
        using var redBrush = new SolidBrush(Color.FromArgb(239, 83, 80));
        using var greenBrush = new SolidBrush(Color.FromArgb(102, 187, 106));
        using var blueBrush = new SolidBrush(Color.FromArgb(66, 165, 245));
        using var orangeBrush = new SolidBrush(Color.FromArgb(255, 167, 38));
        using var purpleBrush = new SolidBrush(Color.FromArgb(171, 71, 188));

        g.FillRectangle(redBrush, 30, 30, 80, 60);
        g.FillRectangle(greenBrush, 70, 50, 80, 60);
        g.FillRectangle(blueBrush, 110, 70, 80, 60);

        // ── Labels ──
        using var labelBrush = new SolidBrush(Color.White);
        var labelFont = new Font("Segoe UI", 10);
        g.DrawString("FillRectangle", labelFont, labelBrush, 30, 145);

        // ── Ellipses ──
        g.FillEllipse(orangeBrush, 250, 30, 100, 100);
        g.FillEllipse(purpleBrush, 300, 50, 80, 80);
        using var ellipseOutline = new Pen(Color.White, 2);
        g.DrawEllipse(ellipseOutline, 270, 20, 120, 120);
        g.DrawString("FillEllipse + DrawEllipse", labelFont, labelBrush, 245, 145);

        // ── Lines ──
        var lineColors = new[]
        {
            Color.FromArgb(239, 83, 80),
            Color.FromArgb(255, 167, 38),
            Color.FromArgb(255, 238, 88),
            Color.FromArgb(102, 187, 106),
            Color.FromArgb(66, 165, 245),
            Color.FromArgb(171, 71, 188),
        };

        for (int i = 0; i < lineColors.Length; i++)
        {
            using var lp = new Pen(lineColors[i], 2 + i * 0.5f);
            g.DrawLine(lp, 470, 30 + i * 18, 630, 30 + i * 18);
        }
        g.DrawString("DrawLine (varied widths)", labelFont, labelBrush, 470, 145);

        // ── Outlined rectangles ──
        using var outlinePen1 = new Pen(Color.FromArgb(38, 198, 218), 2);
        using var outlinePen2 = new Pen(Color.FromArgb(255, 238, 88), 2);
        using var outlinePen3 = new Pen(Color.FromArgb(239, 83, 80), 2);

        g.DrawRectangle(outlinePen1, 680, 30, 100, 50);
        g.DrawRectangle(outlinePen2, 700, 50, 100, 50);
        g.DrawRectangle(outlinePen3, 720, 70, 100, 50);
        g.DrawString("DrawRectangle", labelFont, labelBrush, 695, 145);

        // ── Big text demo ──
        var bigFont = new Font("Segoe UI", 24, FontStyle.Bold);
        using var accentBrush = new SolidBrush(Color.FromArgb(0, 188, 212));
        g.DrawString("Impeller-Powered", bigFont, accentBrush, 30, 190);

        var medFont = new Font("Segoe UI", 16);
        using var subBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
        g.DrawString("GPU-accelerated rendering via DisplayListBuilder", medFont, subBrush, 30, 230);

        // ── Color palette ──
        var palette = new[]
        {
            Color.FromArgb(244, 67, 54), Color.FromArgb(233, 30, 99), Color.FromArgb(156, 39, 176),
            Color.FromArgb(103, 58, 183), Color.FromArgb(63, 81, 181), Color.FromArgb(33, 150, 243),
            Color.FromArgb(3, 169, 244), Color.FromArgb(0, 188, 212), Color.FromArgb(0, 150, 136),
            Color.FromArgb(76, 175, 80), Color.FromArgb(139, 195, 74), Color.FromArgb(205, 220, 57),
            Color.FromArgb(255, 235, 59), Color.FromArgb(255, 193, 7), Color.FromArgb(255, 152, 0),
            Color.FromArgb(255, 87, 34),
        };

        g.DrawString("Material Palette:", labelFont, labelBrush, 30, 280);
        for (int i = 0; i < palette.Length; i++)
        {
            using var pb = new SolidBrush(palette[i]);
            g.FillRectangle(pb, 30 + i * 50, 300, 46, 30);
        }

        base.OnPaint(e);
    }
}


