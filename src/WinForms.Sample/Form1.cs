#pragma warning disable SA1513
using System.Drawing;
using System.Windows.Forms;

namespace WinForms.Sample;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.FillRectangle(Brushes.Red, new Rectangle(10, 10, 100, 100));
        e.Graphics.DrawString("Hello from Impeller! ", this.Font, Brushes.Black, 10, 120);
    }
}
