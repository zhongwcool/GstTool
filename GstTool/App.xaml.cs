using System.Windows;
using GstTool.Utils;

namespace GstTool
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Log.SetLevel(Level.D);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            //强制输出缓存中日志
            Log.Close();

            base.OnExit(e);
        }
    }
}