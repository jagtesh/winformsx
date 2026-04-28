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
        await Task.Delay(-1);
#else
        await Task.CompletedTask;
#endif
    }
}

internal sealed class KitchenSinkForm : Form
{
    private readonly ToolStripStatusLabel _status;

    public KitchenSinkForm()
    {
        Text = "WinFormsX — Kitchen Sink Demo";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(980, 700);
        MinimumSize = new Size(860, 560);
        BackColor = Color.FromArgb(30, 36, 48);

        MenuStrip menu = new();
        menu.Items.Add("File");
        menu.Items.Add("Edit");
        menu.Items.Add("View");
        menu.Items.Add("Help");

        StatusStrip statusStrip = new();
        _status = new ToolStripStatusLabel("Ready");
        statusStrip.Items.Add(_status);

        TabControl tabs = new() { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildBasicsTab());
        tabs.TabPages.Add(BuildListsTab());
        tabs.TabPages.Add(BuildLayoutTab());
        tabs.TabPages.Add(BuildInputsTab());
        tabs.TabPages.Add(BuildProgressTab());

        Controls.Add(tabs);
        Controls.Add(menu);
        Controls.Add(statusStrip);
        MainMenuStrip = menu;
    }

    private TabPage BuildBasicsTab()
    {
        TabPage page = new("Basics")
        {
            BackColor = Color.FromArgb(38, 46, 61)
        };

        Label title = new()
        {
            Text = "Basic Controls",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 18)
        };

        Label subtitle = new()
        {
            Text = "Impeller control renderer (PAL-only path)",
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = Color.FromArgb(200, 212, 235),
            AutoSize = true,
            Location = new Point(22, 52)
        };

        Button primary = new()
        {
            Text = "Primary Action",
            BackColor = Color.FromArgb(74, 139, 255),
            ForeColor = Color.White,
            Size = new Size(168, 42),
            Location = new Point(22, 94)
        };
        primary.Click += (_, _) => SetStatus("Primary button clicked");

        Button secondary = new()
        {
            Text = "Secondary",
            BackColor = Color.FromArgb(85, 93, 108),
            ForeColor = Color.White,
            Size = new Size(132, 42),
            Location = new Point(202, 94)
        };
        secondary.Click += (_, _) => SetStatus("Secondary button clicked");

        GroupBox group = new()
        {
            Text = "Credentials",
            ForeColor = Color.White,
            Size = new Size(430, 176),
            Location = new Point(22, 156)
        };

        Label userLabel = new() { Text = "User", ForeColor = Color.White, AutoSize = true, Location = new Point(18, 38) };
        TextBox user = new() { Text = "jagtesh", Size = new Size(220, 28), Location = new Point(18, 58) };
        user.TextChanged += (_, _) => SetStatus($"User changed: {user.Text}");

        Label passLabel = new() { Text = "Password", ForeColor = Color.White, AutoSize = true, Location = new Point(18, 96) };
        TextBox pass = new() { Text = "secret", PasswordChar = '•', Size = new Size(220, 28), Location = new Point(18, 116) };

        group.Controls.Add(userLabel);
        group.Controls.Add(user);
        group.Controls.Add(passLabel);
        group.Controls.Add(pass);

        Panel accent = new()
        {
            BackColor = Color.FromArgb(77, 163, 255),
            Size = new Size(220, 80),
            Location = new Point(480, 94)
        };

        Label accentText = new()
        {
            Text = "Panel",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(70, 28)
        };
        accent.Controls.Add(accentText);

        page.Controls.Add(title);
        page.Controls.Add(subtitle);
        page.Controls.Add(primary);
        page.Controls.Add(secondary);
        page.Controls.Add(group);
        page.Controls.Add(accent);
        return page;
    }

    private TabPage BuildListsTab()
    {
        TabPage page = new("Lists & Combos")
        {
            BackColor = Color.FromArgb(36, 44, 58)
        };

        Label comboLabel = new()
        {
            Text = "ComboBox:",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 28)
        };

        ComboBox combo = new()
        {
            Location = new Point(24, 50),
            Size = new Size(260, 30),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.Items.AddRange(["Apple", "Banana", "Cherry", "Dragonfruit", "Elderberry"]);
        combo.SelectedIndex = 0;
        combo.SelectedIndexChanged += (_, _) => SetStatus($"Combo selected: {combo.SelectedItem}");

        Label listLabel = new()
        {
            Text = "ListBox:",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 100)
        };

        ListBox list = new()
        {
            Location = new Point(24, 122),
            Size = new Size(300, 240)
        };
        list.Items.AddRange(
        [
            "Alpha Team", "Beta Team", "Gamma Team", "Delta Team", "Epsilon Team", "Zeta Team", "Eta Team", "Theta Team"
        ]);
        list.SelectedIndex = 0;
        list.SelectedIndexChanged += (_, _) => SetStatus($"List selected: {list.SelectedItem}");

        Button add = new()
        {
            Text = "Add Item",
            BackColor = Color.FromArgb(74, 139, 255),
            ForeColor = Color.White,
            Location = new Point(348, 122),
            Size = new Size(130, 36)
        };
        add.Click += (_, _) =>
        {
            list.Items.Add($"New Item {list.Items.Count + 1}");
            SetStatus("Added list item");
        };

        page.Controls.Add(comboLabel);
        page.Controls.Add(combo);
        page.Controls.Add(listLabel);
        page.Controls.Add(list);
        page.Controls.Add(add);
        return page;
    }

    private TabPage BuildLayoutTab()
    {
        TabPage page = new("Layout")
        {
            BackColor = Color.FromArgb(34, 42, 55)
        };

        Panel card1 = new()
        {
            BackColor = Color.FromArgb(51, 94, 155),
            Location = new Point(24, 26),
            Size = new Size(250, 120)
        };

        Panel card2 = new()
        {
            BackColor = Color.FromArgb(71, 148, 103),
            Location = new Point(290, 26),
            Size = new Size(250, 120)
        };

        Panel card3 = new()
        {
            BackColor = Color.FromArgb(163, 99, 58),
            Location = new Point(556, 26),
            Size = new Size(250, 120)
        };

        card1.Controls.Add(new Label { Text = "Dock/Panel A", ForeColor = Color.White, AutoSize = true, Location = new Point(14, 16) });
        card2.Controls.Add(new Label { Text = "Dock/Panel B", ForeColor = Color.White, AutoSize = true, Location = new Point(14, 16) });
        card3.Controls.Add(new Label { Text = "Dock/Panel C", ForeColor = Color.White, AutoSize = true, Location = new Point(14, 16) });

        GroupBox metrics = new()
        {
            Text = "Metrics",
            ForeColor = Color.White,
            Location = new Point(24, 176),
            Size = new Size(420, 210)
        };

        metrics.Controls.Add(new Label { Text = "CPU: 42%", ForeColor = Color.White, AutoSize = true, Location = new Point(18, 42) });
        metrics.Controls.Add(new Label { Text = "GPU: 61%", ForeColor = Color.White, AutoSize = true, Location = new Point(18, 74) });
        metrics.Controls.Add(new Label { Text = "Memory: 7.2 GB", ForeColor = Color.White, AutoSize = true, Location = new Point(18, 106) });

        page.Controls.Add(card1);
        page.Controls.Add(card2);
        page.Controls.Add(card3);
        page.Controls.Add(metrics);
        return page;
    }

    private TabPage BuildInputsTab()
    {
        TabPage page = new("Inputs")
        {
            BackColor = Color.FromArgb(35, 43, 57)
        };

        CheckBox chkA = new() { Text = "Enable notifications", ForeColor = Color.White, Location = new Point(24, 36), AutoSize = true, Checked = true };
        CheckBox chkB = new() { Text = "Auto-save", ForeColor = Color.White, Location = new Point(24, 66), AutoSize = true };
        chkA.CheckedChanged += (_, _) => SetStatus($"Notifications: {chkA.Checked}");
        chkB.CheckedChanged += (_, _) => SetStatus($"Auto-save: {chkB.Checked}");

        RadioButton rb1 = new() { Text = "Daily", ForeColor = Color.White, Location = new Point(24, 118), AutoSize = true, Checked = true };
        RadioButton rb2 = new() { Text = "Weekly", ForeColor = Color.White, Location = new Point(24, 146), AutoSize = true };
        RadioButton rb3 = new() { Text = "Monthly", ForeColor = Color.White, Location = new Point(24, 174), AutoSize = true };
        rb1.CheckedChanged += (_, _) => { if (rb1.Checked) SetStatus("Schedule: Daily"); };
        rb2.CheckedChanged += (_, _) => { if (rb2.Checked) SetStatus("Schedule: Weekly"); };
        rb3.CheckedChanged += (_, _) => { if (rb3.Checked) SetStatus("Schedule: Monthly"); };

        GroupBox notes = new()
        {
            Text = "Notes",
            ForeColor = Color.White,
            Location = new Point(240, 36),
            Size = new Size(420, 220)
        };

        TextBox noteText = new()
        {
            Text = "Type here...",
            Location = new Point(18, 42),
            Size = new Size(370, 28)
        };
        noteText.TextChanged += (_, _) => SetStatus("Notes changed");

        notes.Controls.Add(noteText);

        page.Controls.Add(chkA);
        page.Controls.Add(chkB);
        page.Controls.Add(rb1);
        page.Controls.Add(rb2);
        page.Controls.Add(rb3);
        page.Controls.Add(notes);
        return page;
    }

    private TabPage BuildProgressTab()
    {
        TabPage page = new("Status")
        {
            BackColor = Color.FromArgb(37, 44, 58)
        };

        Label heading = new()
        {
            Text = "Renderer Verification",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 24)
        };

        Label info = new()
        {
            Text = "This tab confirms each control type is drawn through the Impeller primitive renderer.",
            ForeColor = Color.FromArgb(196, 210, 232),
            AutoSize = true,
            Location = new Point(24, 56)
        };

        Button action = new()
        {
            Text = "Mark Verified",
            BackColor = Color.FromArgb(74, 139, 255),
            ForeColor = Color.White,
            Location = new Point(24, 98),
            Size = new Size(160, 40)
        };
        action.Click += (_, _) => SetStatus("Renderer verification clicked");

        page.Controls.Add(heading);
        page.Controls.Add(info);
        page.Controls.Add(action);
        return page;
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }
}
