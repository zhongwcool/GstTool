using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using GLib;
using Gst;
using Gst.Video;
using Application = Gst.Application;
using Debug = System.Diagnostics.Debug;
using EventArgs = System.EventArgs;
using ObjectManager = GtkSharp.GstreamerSharp.ObjectManager;
using Value = GLib.Value;

namespace GstTool
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainLoop _mainLoop;

        private Pipeline _pipeline;

        private Element _tee;

        public MainWindow()
        {
            InitializeComponent();

            Application.Init();
            // initialize object manager. otherwise bus subscription will fail
            ObjectManager.Initialize();

            _mainLoop = new MainLoop();
            ThreadPool.QueueUserWorkItem(x => _mainLoop.Run());

            Loaded += (sender, args) =>
            {
                // Launch Method
                //  var pipeline = (Pipeline) Parse.Launch(
                //      "udpsrc ! application/x-rtp ! rtpjitterbuffer ! rtph264depay ! decodebin ! " +
                //      "tee name=t ! queue leaky=1 ! clockoverlay ! d3dvideosink sync=false async=false t. ! queue ! " +
                //      "videoconvert ! x264enc ! mpegtsmux ! filesink location=e:/h3.mp4"
                //      );
                //  var pipeline = (Pipeline) Parse.Launch(
                //      "udpsrc port=5004 ! application/x-rtp ! rtpjitterbuffer ! rtph264depay ! decodebin " +
                //      "! textoverlay text=\"xx科技\" valignment=bottom halignment=left font-desc=\"微软雅黑, 22\" " +
                //      "shaded-background=no ! timeoverlay halignment=left valignment=top ! d3dvideosink sync=false async=false"
                //  );
                // var pipeline = (Pipeline) Parse.Launch("videotestsrc pattern=0 ! videoconvert ! timeoverlay ! d3dvideosink");
                // var pipeline = (Pipeline) Parse.Launch("playbin uri=http://stream.iqilu.com/vod_bag_2016//2020/02/16/903BE158056C44fcA9524B118A5BF230/903BE158056C44fcA9524B118A5BF230_H264_mp4_500K.mp4");
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            var ret = _pipeline?.SetState(State.Null);
            Debug.WriteLine("SetStateNULL returned: " + ret);
            _pipeline?.Dispose();
            _mainLoop.Quit();
            base.OnClosed(e);
        }

        private void OnPadAdded(object sender, PadAddedArgs args)
        {
            var src = (Element) sender;
            var newPad = args.NewPad;

            var newPadCaps = newPad.CurrentCaps;
            var newPadStruct = newPadCaps.GetStructure(0);
            var newPadType = newPadStruct.Name;

            if (newPadType.StartsWith("video/x-raw"))
            {
                var sinkPad = _tee.GetStaticPad("sink");
                Debug.WriteLine($"Received new pad '{newPad.Name}' from '{src.Name}':");

                if (sinkPad.IsLinked)
                {
                    Debug.WriteLine("We are already linked, ignoring");
                    return;
                }

                var ret = newPad.Link(sinkPad);
                if (ret == PadLinkReturn.Ok)
                    Debug.WriteLine($"Link succeeded type {newPadType}");
                else
                    Debug.WriteLine($"Type is {newPadType} but link failed");
            }
            else
            {
                Debug.WriteLine($"It has type '{newPadType}' which is not raw audio or video. Ignoring.");
            }

            newPadCaps.Dispose();
        }

        private void Bus_Message(object sender, MessageArgs args)
        {
            var msg = args.Message;

            switch (msg.Type)
            {
                case MessageType.Eos:
                    Console.WriteLine("End of stream reached");
                    if (_mainLoop.IsRunning) _mainLoop.Quit();
                    break;
                case MessageType.Error:
                    Debug.WriteLine("Bus_Message: Error received: " + msg);
                    if (_mainLoop.IsRunning) _mainLoop.Quit();
                    break;
                case MessageType.StreamStatus:
                    msg.ParseStreamStatus(out var status, out var theOwner);
                    Debug.WriteLine("Bus_Message: 流状态: " + status + " ; Owner is: " + theOwner.Name);
                    break;
                case MessageType.StateChanged:
                    msg.ParseStateChanged(out var oldState, out var newState, out var pendingState);
                    if (newState == State.Paused) args.RetVal = false;
                    Debug.WriteLine("Bus_Message: 链路状态 from {0} to {1} ; Pending: {2}",
                        Element.StateGetName(oldState),
                        Element.StateGetName(newState),
                        Element.StateGetName(pendingState)
                    );
                    break;
                case MessageType.Element:
                    HandleElement(msg);
                    break;
            }

            args.RetVal = true;
        }

        [DllImport("gobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern double g_value_get_double(ref Value val);

        [DllImport("gobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void g_value_array_free(IntPtr raw);

        [DllImport("gobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr g_value_get_boxed(ref Value val);

        [DllImport("glib-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void g_free(IntPtr mem);

        [DllImport("gstreamer-1.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gst_structure_get_value(IntPtr raw, IntPtr fieldname);

        [DllImport("gobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void g_value_unset(ref object val);

        private static void HandleElement(Message msg)
        {
            Console.Write(".");
            var msgStruct = msg.Structure;
            var elName = msgStruct.Name;
            if (elName != "level") return;
            var rmsValue = msgStruct.GetValue("rms");
            var handle = g_value_get_boxed(ref rmsValue);
            g_value_array_free(handle);
            /*
            at Gst.MiniObject.gst_mini_object_unref(IntPtr raw)
               at Gst.MiniObject.FinalizerInfo.Handler()
               at GLib.Timeout.TimeoutProxy.Handler()
               at GLib.MainLoop.g_main_loop_run(IntPtr loop)
            */
        }

        private void ButtonPlay_OnClick(object sender, RoutedEventArgs e)
        {
            // ElementFactory Method
            var source = ElementFactory.Make("udpsrc");
            var sourceBuffer = ElementFactory.Make("rtpjitterbuffer");
            var sourceDepay = ElementFactory.Make("rtph264depay");
            var sourceDecode = ElementFactory.Make("decodebin");
            _tee = ElementFactory.Make("tee");
            var videoQueue = ElementFactory.Make("queue");
            var videoOverlay = ElementFactory.Make("clockoverlay");
            var videoConvert = ElementFactory.Make("videoconvert");
            var videoSink = ElementFactory.Make("d3dvideosink");
            var fileQueue = ElementFactory.Make("queue");
            var fileConvert = ElementFactory.Make("videoconvert");
            var fileEncode = ElementFactory.Make("x264enc");
            var fileMux = ElementFactory.Make("mpegtsmux");
            var fileSink = ElementFactory.Make("filesink");

            _pipeline = new Pipeline("test-pipeline");

            if (new[]
            {
                source, sourceBuffer, sourceDepay, sourceDecode, _tee, videoQueue, videoConvert,
                videoSink, fileQueue, fileConvert, fileEncode, fileMux, fileSink
            }.Any(e => e == null))
            {
                "Not all elements could be created".PrintErr();
                return;
            }

            source["caps"] = new Caps("application/x-rtp");
            fileSink["location"] = "./h6.mp4";
            videoSink["sync"] = false;
            videoSink["async"] = false;
            videoQueue["leaky"] = 1;
            fileQueue["leaky"] = 1;

            _pipeline.Add(source, sourceBuffer, sourceDepay, sourceDecode, _tee, videoQueue,
                videoConvert, videoSink, fileQueue, fileConvert, fileEncode, fileMux, fileSink);

            /* Link all elements that can be automatically linked because they have "Always" pads */
            if (!Element.Link(source, sourceBuffer, sourceDepay, sourceDecode) ||
                !Element.Link(videoQueue, videoConvert, videoSink) ||
                !Element.Link(fileQueue, fileConvert, fileEncode, fileMux, fileSink))
            {
                "Elements could not be linked".PrintErr();
                return;
            }

            sourceDecode.PadAdded += OnPadAdded;

            var teeVideoPad = _tee.GetRequestPad("src_%u");
            Debug.WriteLine($"Obtained request pad {teeVideoPad.Name} for video branch.");
            var teeFilePad = _tee.GetRequestPad("src_%u"); // from gst-inspect
            Debug.WriteLine($"Obtained request pad {teeFilePad.Name} for file branch.");

            var queueVideoPad = videoQueue.GetStaticPad("sink");
            var queueFilePad = fileQueue.GetStaticPad("sink");

            if (teeVideoPad.Link(queueVideoPad) != PadLinkReturn.Ok ||
                teeFilePad.Link(queueFilePad) != PadLinkReturn.Ok)
            {
                "Tee could not be linked".PrintErr();
                return;
            }

            // subscribe to the messaging system of the bus and pipeline so we can monitor status as we go
            var bus = _pipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += Bus_Message;

            // Start playing the pipeline
            var overlay = _pipeline.GetByInterface(VideoOverlayAdapter.GType);
            var adapter = new VideoOverlayAdapter(overlay.Handle)
            {
                WindowHandle = VideoPanel.Handle
            };
            adapter.HandleEvents(true);

            // finally set the state of the pipeline running so we can get data
            //var ret = _pipeline.SetState(State.Ready);
            //Debug.WriteLine("SetStateReady returned: " + ret);
            var ret = _pipeline.SetState(State.Playing);
            Debug.WriteLine("SetStatePlaying returned: " + ret);

            ButtonPrepare.IsEnabled = false;
        }
    }
}