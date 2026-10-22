using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;

namespace WinUiSpike;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.DrawText("Win2D CanvasControl rendering", 20, 20, Microsoft.UI.Colors.White);
        args.DrawingSession.FillRectangle(20, 60, 240, 80, Microsoft.UI.Colors.SteelBlue);
    }
}
