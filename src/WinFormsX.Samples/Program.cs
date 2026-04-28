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
        Application.Run(new SolidBackgroundForm());
#if BROWSER
        await Task.Delay(-1);
#else
        await Task.CompletedTask;
#endif
    }
}

internal sealed class SolidBackgroundForm : Form
{
    internal const string HarnessWindowTitle = "WINFORMSX_AUTOMATION_WINDOW";

    public SolidBackgroundForm()
    {
        Text = HarnessWindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 600);
        BackColor = Color.FromArgb(32, 36, 44);

        Label marker = new()
        {
            AutoSize = true,
            Text = "WINFORMSX_RENDER_OK",
            ForeColor = Color.FromArgb(230, 235, 245),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            Location = new Point(24, 24)
        };

        Panel swatch = new()
        {
            BackColor = Color.FromArgb(77, 163, 255),
            Size = new Size(220, 80),
            Location = new Point(30, 95)
        };

        Controls.Add(marker);
        Controls.Add(swatch);
    }
}
