using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GLib;
using Gst;
using Gst.Video;
using Application = Gst.Application;
using Debug = System.Diagnostics.Debug;
using EventArgs = System.EventArgs;
using Global = Gst.Video.Global;

namespace GstTool
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainLoop _mainLoop;

        private Pipeline _pipeline;

        //private Element tee;
        private Element _videoConvert;

        public MainWindow()
        {
            InitializeComponent();

            Application.Init();
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

                // ElementFactory Method
                var source = ElementFactory.Make("udpsrc", "video_source");
                var sourceBuffer = ElementFactory.Make("rtpjitterbuffer", "source_buffer");
                var sourceDepay = ElementFactory.Make("rtph264depay", "source_depay");
                var sourceDecode = ElementFactory.Make("decodebin", "source_decode");
                // tee = ElementFactory.Make("tee", "tee");
                // var videoQueue = ElementFactory.Make("queue", "video_queue");
                // var videoOverlay = ElementFactory.Make("clockoverlay", "video_overlay");
                _videoConvert = ElementFactory.Make("videoconvert", "csp");
                var videoSink = ElementFactory.Make("d3dvideosink", "video_sink");
                // var fileQueue = ElementFactory.Make("queue", "file_queue");
                // var fileConvert = ElementFactory.Make("videoconvert", "file_convert");
                // var fileEncode = ElementFactory.Make("x264enc", "file_encode");
                // var fileMux = ElementFactory.Make("mpegtsmux", "file_mux");
                // var fileSink = ElementFactory.Make("filesink", "file_sink");

                _pipeline = new Pipeline("test-pipeline");

                if (new[]
                {
                    source, sourceBuffer, sourceDepay, sourceDecode, _videoConvert,
                    videoSink
                }.Any(e => e == null))
                {
                    "Not all elements could be created".PrintErr();
                    return;
                }

                source["caps"] = new Caps("application/x-rtp");
                // fileSink["location"] = "e:/h6.mp4";
                // videoQueue["leaky"] = 1;
                // fileQueue["leaky"] = 1;

                _pipeline.Add(source, sourceBuffer, sourceDepay, sourceDecode, _videoConvert, videoSink);

                /* Link all elements that can be automatically linked because they have "Always" pads */
                if (!Element.Link(source, sourceBuffer, sourceDepay, sourceDecode) ||
                    !Element.Link(_videoConvert, videoSink))
                {
                    "Elements could not be linked".PrintErr();
                    return;
                }

                sourceDecode.PadAdded += OnPadAdded;

                /*
                var teeVideoPad = tee.GetRequestPad("src_%u");
                Debug.WriteLine($"Obtained request pad {teeVideoPad.Name} for video branch.");
                var teeFilePad = tee.GetRequestPad("src_%u"); // from gst-inspect
                Debug.WriteLine($"Obtained request pad {teeFilePad.Name} for file branch.");

                var queueVideoPad = videoQueue.GetStaticPad("sink");
                var queueFilePad = fileQueue.GetStaticPad("sink");

                if (teeVideoPad.Link(queueVideoPad) != PadLinkReturn.Ok ||
                    teeFilePad.Link(queueFilePad) != PadLinkReturn.Ok)
                {
                    "Tee could not be linked".PrintErr();
                    return;
                }
                */

                // subscribe to the messaging system of the bus and pipeline so we can monitor status as we go
                var bus = _pipeline.Bus;
                bus.AddSignalWatch();
                bus.Message += OnBusMessage;
                bus.EnableSyncMessageEmission();
                bus.SyncMessage += OnBusSyncMessage;

                // Start playing the pipeline
                var overlay = _pipeline.GetByInterface(VideoOverlayAdapter.GType);
                var adapter = new VideoOverlayAdapter(overlay.Handle)
                {
                    WindowHandle = VideoPanel.Handle
                };
                adapter.HandleEvents(true);

                // finally set the state of the pipeline running so we can get data
                var ret = _pipeline.SetState(State.Null);
                Debug.WriteLine("SetStateNULL returned: " + ret);
                ret = _pipeline.SetState(State.Ready);
                Debug.WriteLine("SetStateReady returned: " + ret);
                ret = _pipeline.SetState(State.Playing);
                Debug.WriteLine("SetStatePlaying returned: " + ret);
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            var ret = _pipeline.SetState(State.Null);
            Debug.WriteLine("SetStateNULL returned: " + ret);
            _pipeline.Dispose();
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
                var sinkPad = _videoConvert.GetStaticPad("sink");
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

        private static void OnBusSyncMessage(object o, SyncMessageArgs args)
        {
            if (!Global.IsVideoOverlayPrepareWindowHandleMessage(args.Message)) return;

            if (args.Message.Src is VideoSink or Bin)
                try
                {
                    args.Message.Src["force-aspect-ratio"] = true;
                }
                catch (PropertyNotFoundException exception)
                {
                    Debug.WriteLine(exception);
                }
        }

        private static void OnBusMessage(object o, MessageArgs args)
        {
            var msg = args.Message;

            switch (msg.Type)
            {
                case MessageType.Eos:
                    Console.WriteLine("End of stream reached");
                    break;
                case MessageType.Error:
                    Debug.WriteLine("OnBusMessage: Error received: " + msg);
                    break;
                case MessageType.StreamStatus:
                    msg.ParseStreamStatus(out var status, out var theOwner);
                    Debug.WriteLine("OnBusMessage: 流状态: " + status + " ; Owner is: " + theOwner.Name);
                    break;
                case MessageType.StateChanged:
                    msg.ParseStateChanged(out var oldState, out var newState, out var pendingState);
                    if (newState == State.Paused) args.RetVal = false;
                    Debug.WriteLine("OnBusMessage: 链路状态 from {0} to {1} ; Pending: {2}",
                        Element.StateGetName(oldState),
                        Element.StateGetName(newState),
                        Element.StateGetName(pendingState)
                    );
                    break;
            }

            args.RetVal = true;
        }
    }
}