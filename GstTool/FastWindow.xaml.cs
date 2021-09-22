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
                    "videotestsrc pattern=1 ! video/x-raw,width=1280,height=720 ! tee name=t ! queue " +
                    "leaky=1 ! videoconvert ! d3dvideosink sync=false async=false t. ! queue ! videoconvert ! x264enc ! " +
                    "mpegtsmux ! filesink location=e:/testvideo.mp4"
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