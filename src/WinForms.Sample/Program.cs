#pragma warning disable SA1513
using System.Windows.Forms;

namespace WinForms.Sample;

internal static class Program
{
    [System.STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
    }
}
