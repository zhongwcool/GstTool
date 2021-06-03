using System.Windows;
using Gst;
using Gst.Video;
using Debug = System.Diagnostics.Debug;

namespace GstTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            Gst.Application.Init();
            var mainLoop = new GLib.MainLoop();
            System.Threading.ThreadPool.QueueUserWorkItem(x => mainLoop.Run());
            
            Loaded += (sender, args) =>
            {
                var pipeline = (Pipeline) Parse.Launch("udpsrc port=5004 ! application/x-rtp ! rtpjitterbuffer ! rtph264depay ! decodebin ! textoverlay text=\"xx科技\" valignment=bottom halignment=left font-desc=\"微软雅黑, 22\" shaded-background=no ! timeoverlay halignment=left valignment=top ! d3dvideosink sync=false async=false");
                //var pipeline = (Pipeline) Parse.Launch("videotestsrc pattern=0 ! videoconvert ! timeoverlay ! d3dvideosink");
                //var pipeline = (Pipeline) Parse.Launch("playbin uri=http://stream.iqilu.com/vod_bag_2016//2020/02/16/903BE158056C44fcA9524B118A5BF230/903BE158056C44fcA9524B118A5BF230_H264_mp4_500K.mp4");

                // subscribe to the messaging system of the bus and pipeline so we can monitor status as we go
                // var bus = pipeline.Bus;
                // bus.AddSignalWatch();
                // bus.Message += Bus_Message;

                // bus.EnableSyncMessageEmission();
                // bus.SyncMessage += Bus_SyncMessage;
                
                var overlay= pipeline.GetByInterface(VideoOverlayAdapter.GType);
                var adapter = new VideoOverlayAdapter(overlay.Handle)
                {
                    WindowHandle = VideoPanel.Handle
                };
                adapter.HandleEvents(true);

                pipeline.SetState(State.Playing);
            };
        }
        
        private void Bus_SyncMessage(object o, SyncMessageArgs args)
        {
            if (Gst.Video.Global.IsVideoOverlayPrepareWindowHandleMessage(args.Message))
            {
                Debug.WriteLine("Bus_SyncMessage: Message prepare window handle received by: " + args.Message.Src.Name +
                                " " + args.Message.Src.GetType());
                Debug.WriteLine("");

                try
                {
                    args.Message.Src["force-aspect-ratio"] = true;
                }
                catch (PropertyNotFoundException exception)
                {
                    Debug.WriteLine(exception);
                }
            }
            else
            {
                args.Message.ParseInfo(out _, out var info);
                Debug.WriteLine("Bus_SyncMessage: " + args.Message.Type + " - " + info);
            }
        }

        private void Bus_Message(object o, MessageArgs args)
        {
            var msg = args.Message;
            
            switch (msg.Type)
            {
                case MessageType.Error:
                    Debug.WriteLine("Bus_Message: Error received: " + msg);
                    break;
                case MessageType.StreamStatus:
                    msg.ParseStreamStatus(out var status, out var theOwner);
                    Debug.WriteLine("Bus_Message: 流状态: " + status + " ; Owner is: " +
                                    theOwner.Name);
                    break;
                case MessageType.StateChanged:
                    msg.ParseStateChanged(out var oldState, out var newState, out var pendingState);
                    if (newState == State.Paused) args.RetVal = false;
                    Debug.WriteLine("Bus_Message: 链路状态 from {0} to {1}: ; Pending: {2}",
                        Element.StateGetName(oldState), 
                        Element.StateGetName(newState),
                        Element.StateGetName(pendingState)
                        );
                    break;
                case MessageType.Element:
                    Debug.WriteLine("Bus_Message: Element message: {0}", args.Message.ToString());
                    break;
                default:
                    Debug.WriteLine("Bus_Message: 未处理: {0}", msg.Type);
                    break;
            }

            args.RetVal = true;
        }
    }
}