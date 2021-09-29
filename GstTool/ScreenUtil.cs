using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Image = System.Drawing.Image;
using Panel = System.Windows.Forms.Panel;
using Point = System.Windows.Point;

namespace GstTool
{
    public class ScreenUtil
    {
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource,
            int xSrc, int ySrc, CopyPixelOperation rop);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteObject(IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr ptr);

        public static Bitmap Capture(int left, int top, int width, int height)
        {
            var hDesk = GetDesktopWindow();
            var hSrce = GetWindowDC(hDesk);
            var hDest = CreateCompatibleDC(hSrce);
            var hBmp = CreateCompatibleBitmap(hSrce, width, height);
            var hOldBmp = SelectObject(hDest, hBmp);
            BitBlt(hDest, 0, 0, width, height, hSrce, left, top,
                CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
            var bmp = Image.FromHbitmap(hBmp);
            SelectObject(hDest, hOldBmp);
            DeleteObject(hBmp);
            DeleteDC(hDest);
            ReleaseDC(hDesk, hSrce);
            return bmp;
        }

        public static void Grab(Control control, string path)
        {
            var topLeft = control.PointToScreen(new Point(0, 0));
            var bitmap = Capture(
                (int)topLeft.X,
                (int)topLeft.Y,
                (int)control.ActualWidth,
                (int)control.ActualHeight);
            bitmap.Save(path);
        }

        public static void Grab(Panel control, string path)
        {
            var topLeft = control.PointToScreen(new System.Drawing.Point(0, 0));
            var bitmap = Capture(
                topLeft.X,
                topLeft.Y,
                control.Width,
                control.Height);
            bitmap.Save(path);
        }
    }
}