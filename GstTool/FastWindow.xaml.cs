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
                    "queue leaky=2 ! textoverlay text=\"检测信息：\n任务名称：CCTV检测\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\" valignment=0 halignment=0 line-alignment=0 " +
                    "! clockoverlay ! videoconvert ! d3dvideosink sync=false async=false t. ! " +
                    "queue leaky=2 ! textoverlay text=\"检测信息：\n任务名称：CCTV检测\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\n检测地点：星湖街328号9栋6楼\" valignment=0 halignment=0 line-alignment=0 " +
                    "! clockoverlay ! videoconvert ! x264enc ! mpegtsmux ! filesink location=fasttest.mp4"
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