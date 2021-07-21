using System.Windows;
using GstTool.Utils;

namespace GstTool
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            //强制输出缓存中日志
            Log.OnExit();

            base.OnExit(e);
        }
    }
}