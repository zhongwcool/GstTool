using System.Threading;
using System.Windows;
using GLib;
using Gst;
using Gst.Video;
using GstTool.ViewModel;
using Application = Gst.Application;

namespace GstTool
{
    public partial class FastWindow : Window
    {
        public FastWindow()
        {
            InitializeComponent();
            DataContext = FastViewModel.CreateInstance();

            Application.Init();
            var mainLoop = new MainLoop();
            ThreadPool.QueueUserWorkItem(x => mainLoop.Run());

            Loaded += (sender, args) =>
            {
                // Launch Method
                var pipeline = (Pipeline)Parse.Launch(
                    "videotestsrc pattern=0 ! video/x-raw,width=1280,height=720,framerate=600/1 ! tee name=t ! " +
                    "queue leaky=2 ! textoverlay text=\"任务名称：CCTV检测\n检测方向：顺向\n检测地点：临湖界西路\n检测日期：2020年03月11日\n起始井号-终止井号：WSW51-WSW52\n管        径：300mm\n管        材：HDPE双壁波纹管\n管道类型：WS污水管道\n检测单位：苏州蛟视管网有限公司\n检测    员：张三 \" valignment=0 halignment=0 line-alignment=0 " +
                    "! clockoverlay ! videoconvert ! d3dvideosink sync=false async=false t. ! " +
                    "queue leaky=2 ! textoverlay text=\"任务名称：CCTV检测\n检测方向：顺向\n检测地点：临湖界西路\n检测日期：2020年03月11日\n起始井号-终止井号：WSW51-WSW52\n管        径：300mm\n管        材：HDPE双壁波纹管\n管道类型：WS污水管道\n检测单位：苏州蛟视管网有限公司\n检测    员：张三 \" valignment=0 halignment=0 line-alignment=0 " +
                    "! textoverlay text='YYDS' ! clockoverlay ! videoconvert ! x264enc ! mpegtsmux ! filesink location=fasttest.mp4"
                );
                var overlay = pipeline.GetByInterface(VideoOverlayAdapter.GType);
                var adapter = new VideoOverlayAdapter(overlay.Handle)
                {
                    WindowHandle = VideoPanel.Handle
                };
                adapter.HandleEvents(true);

                pipeline.SetState(State.Playing);
            };
        }

        private void ButtonTest_OnClick(object sender, RoutedEventArgs e)
        {
            //PlayStream();
        }
    }
}