using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using GLib;
using Gst;
using Gst.Video;
using GstTool.Utils;
using GstTool.ViewModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using Application = Gst.Application;
using DateTime = System.DateTime;
using EventArgs = System.EventArgs;
using Log = GstTool.Utils.Log;
using Message = GstTool.Model.Message;
using ObjectManager = GtkSharp.GstreamerSharp.ObjectManager;

namespace GstTool
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainLoop _mainLoop;
        private Pipeline _pipeline;
        private Pad _queueVideoPad;
        private Pad _queueFilePad;
        private Element _tee;
        private Pad _teeVideoPad;
        private Pad _teeFilePad;
        private Element _videoQueue;
        private Element _videoOverlayInfo;
        private Element _fileOverlayInfo;
        private Element _fileQueue;
        private Element _fileOverlayClock;
        private Element _fileConvert;
        private Element _fileEncode;
        private Element _fileMux;
        private Element _fileSink;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainViewModel.CreateInstance();

            /* Initialize GStreamer */
            Application.Init();
            /* initialize object manager. otherwise bus subscription will fail */
            ObjectManager.Initialize();

            _mainLoop = new MainLoop();
            ThreadPool.QueueUserWorkItem(x => _mainLoop.Run());

            //注册消息接收器
            WeakReferenceMessenger.Default.Register<Message>(this, OnMessageHandle);
        }

        private void OnMessageHandle(object recipient, Message message)
        {
            switch (message.Key)
            {
                case Message.PlayStream:
                    OnPlayStream();
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var ret = _pipeline?.SetState(State.Null);
            Log.D($"SetState: NULL returned: {ret}");
            _pipeline?.Dispose();
            _mainLoop.Quit();
            base.OnClosed(e);
        }

        private void OnPlayStream()
        {
            PlayStream();
        }

        private void OnPadAdded(object sender, PadAddedArgs args)
        {
            var src = (Element)sender;
            var newPad = args.NewPad;

            var newPadCaps = newPad.CurrentCaps;
            var newPadStruct = newPadCaps.GetStructure(0);
            var newPadType = newPadStruct.Name;

            if (newPadType.StartsWith("video/x-raw"))
            {
                var sinkPad = _tee.GetStaticPad("sink");
                Log.D($"Received new pad '{newPad.Name}' from '{src.Name}':");

                if (sinkPad.IsLinked)
                {
                    Log.D("We are already linked, ignoring");
                    return;
                }

                var ret = newPad.Link(sinkPad);
                if (ret == PadLinkReturn.Ok)
                    Log.D($"Link succeeded type {newPadType}");
                else
                    Log.D($"Type is {newPadType} but link failed");
            }
            else
            {
                Log.D($"It has type '{newPadType}' which is not raw audio or video. Ignoring.");
            }

            newPadCaps.Dispose();
        }

        private void Bus_Message(object sender, MessageArgs args)
        {
            var msg = args.Message;

            switch (msg.Type)
            {
                case MessageType.Eos:
                    Log.W("End of stream reached");
                    if (_mainLoop.IsRunning) _mainLoop.Quit();
                    break;
                case MessageType.Error:
                    Log.W($"Error received: {msg}");
                    if (_mainLoop.IsRunning) _mainLoop.Quit();
                    break;
                case MessageType.StreamStatus:
                    msg.ParseStreamStatus(out var status, out var theOwner);
                    Log.D($"流状态: {status} ; Owner is: {theOwner.Name}");
                    break;
                case MessageType.StateChanged:
                    msg.ParseStateChanged(out var oldState, out var newState, out var pendingState);
                    if (newState == State.Paused) args.RetVal = false;
                    Log.D($"链路状态 from {Element.StateGetName(oldState)} " +
                          $"to {Element.StateGetName(newState)}; Pending: {Element.StateGetName(pendingState)}");
                    break;
                case MessageType.StreamStart:
                    Log.D("!!! StreamStart");
                    ShowTaskInfo();
                    break;
            }

            args.RetVal = true;
        }

        private void PlayStream()
        {
            /* Create the elements */
            var source = ElementFactory.Make("udpsrc");
            var sourceBuffer = ElementFactory.Make("rtpjitterbuffer");
            var sourceDepay = ElementFactory.Make("rtph264depay");
            var sourceDecode = ElementFactory.Make("decodebin");
            _tee = ElementFactory.Make("tee");
            _videoQueue = ElementFactory.Make("queue");
            var videoOverlayClock = ElementFactory.Make("clockoverlay");
            _videoOverlayInfo = ElementFactory.Make("textoverlay");
            var videoConvert = ElementFactory.Make("videoconvert");
            var videoSink = ElementFactory.Make("d3dvideosink");
            _fileQueue = ElementFactory.Make("queue");
            _fileOverlayClock = ElementFactory.Make("clockoverlay");
            _fileOverlayInfo = ElementFactory.Make("textoverlay");
            _fileConvert = ElementFactory.Make("videoconvert");
            _fileEncode = ElementFactory.Make("x264enc");
            _fileMux = ElementFactory.Make("mpegtsmux");
            _fileSink = ElementFactory.Make("filesink");

            /* Create the empty pipeline */
            _pipeline = new Pipeline("test-pipeline");

            if (new[]
            {
                source, sourceBuffer, sourceDepay, sourceDecode, _tee,
                _videoQueue, videoOverlayClock, _videoOverlayInfo, videoConvert, videoSink,
                _fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux, _fileSink
            }.Any(element => element == null))
            {
                Log.E("Not all elements could be created");
                return;
            }

            source["caps"] = new Caps("application/x-rtp");
            _fileSink["location"] = FileUtil.GetRecordFilename();
            videoSink["sync"] = false;
            videoSink["async"] = false;
            _videoQueue["leaky"] = 1;
            _fileQueue["leaky"] = 1;

            _videoOverlayInfo["text"] = "蛟视科技";
            _videoOverlayInfo["valignment"] = 0;
            _videoOverlayInfo["halignment"] = 0;
            _videoOverlayInfo["line-alignment"] = 0;

            _fileOverlayInfo["text"] = "蛟视科技";
            _fileOverlayInfo["valignment"] = 0;
            _fileOverlayInfo["halignment"] = 0;
            _fileOverlayInfo["line-alignment"] = 0;

            _pipeline.Add(source, sourceBuffer, sourceDepay, sourceDecode, _tee,
                _videoQueue, videoOverlayClock, _videoOverlayInfo, videoConvert, videoSink,
                _fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux, _fileSink);

            /* Link all elements that can be automatically linked because they have "Always" pads */
            if (!Element.Link(source, sourceBuffer, sourceDepay, sourceDecode) ||
                !Element.Link(_videoQueue, _videoOverlayInfo, videoOverlayClock, videoConvert, videoSink) ||
                !Element.Link(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                    _fileSink))
            {
                Log.E("Elements could not be linked");
                return;
            }

            sourceDecode.PadAdded += OnPadAdded;

            _teeVideoPad = _tee.GetRequestPad("src_%u");
            Log.D($"Obtained request pad {_teeVideoPad.Name} for video branch.");
            _teeFilePad = _tee.GetRequestPad("src_%u"); // from gst-inspect
            Log.D($"Obtained request pad {_teeFilePad.Name} for file branch.");

            _queueVideoPad = _videoQueue.GetStaticPad("sink");
            _queueFilePad = _fileQueue.GetStaticPad("sink");

            if (_teeVideoPad.Link(_queueVideoPad) != PadLinkReturn.Ok ||
                _teeFilePad.Link(_queueFilePad) != PadLinkReturn.Ok)
            {
                Log.E("Tee could not be linked");
                return;
            }

            /* subscribe to the messaging system of the bus and pipeline so we can monitor status as we go */
            var bus = _pipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += Bus_Message;

            /* start playing the pipeline */
            var overlay = _pipeline.GetByInterface(VideoOverlayAdapter.GType);
            var adapter = new VideoOverlayAdapter(overlay.Handle)
            {
                WindowHandle = VideoPanel.Handle
            };
            adapter.HandleEvents(true);

            /* finally set the state of the pipeline running so we can get data */
            //var ret = _pipeline.SetState(State.Ready);
            //Log.D("SetStateReady returned: " + ret);
            var ret = _pipeline.SetState(State.Playing);
            Log.D($"SetStatePlaying returned: {ret}");

            ButtonPrepare.IsEnabled = false;
        }

        private void ButtonShot_OnClick(object sender, RoutedEventArgs e)
        {
            ScreenUtil.Grab(VideoPanel, FileUtil.GetShotFilename());
        }

        private void ButtonTest_OnClick(object sender, RoutedEventArgs e)
        {
            // DO Unlink
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Null);
            if (!_teeFilePad.Unlink(_queueFilePad))
            {
                MessageBox.Show("Failed to Unlink Video Pad.");
                ButtonLink.IsEnabled = false;
                return;
            }

            Element.Unlink(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink);

            // DO Link
            _fileSink["location"] = FileUtil.GetRecordFilename();
            if (!Element.Link(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink))
            {
                Log.E("Elements could not be linked");
                return;
            }

            _queueFilePad = _fileQueue.GetStaticPad("sink");
            if (_teeFilePad.Link(_queueFilePad) != PadLinkReturn.Ok)
            {
                MessageBox.Show("Failed to Link Video Pad.");
                return;
            }

            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Playing);
        }

        private void ButtonUnlink_OnClick(object sender, RoutedEventArgs e)
        {
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Null);
            if (!_teeFilePad.Unlink(_queueFilePad))
            {
                MessageBox.Show("Failed to Unlink Video Pad.");
                ButtonLink.IsEnabled = false;
                return;
            }

            Element.Unlink(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink);

            ButtonLink.IsEnabled = true;
            ButtonUnlink.IsEnabled = false;
        }

        private void ButtonLink_OnClick(object sender, RoutedEventArgs e)
        {
            _fileSink["location"] = FileUtil.GetRecordFilename();
            if (!Element.Link(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink))
            {
                Log.E("Elements could not be linked");
                return;
            }

            _queueFilePad = _fileQueue.GetStaticPad("sink");
            if (_teeFilePad.Link(_queueFilePad) != PadLinkReturn.Ok)
            {
                MessageBox.Show("Failed to Link Video Pad.");
                return;
            }

            _pipeline.SetState(State.Playing);

            ButtonUnlink.IsEnabled = true;
            ButtonLink.IsEnabled = false;
        }

        private void ButtonTask_OnClick(object sender, RoutedEventArgs e)
        {
            ShowTaskInfo();
        }

        private void ShowTaskInfo()
        {
            Dispatcher.Invoke(() =>
            {
                var renWu = "CCTV检测";
                var fangXiang = "顺向";
                var diDian = "星湖街328号";
                var riQi = "2021年03月16日";
                var qiShiJingHao = "未填写";
                var zhongZhiJingHao = "未填写";
                var guanJing = "300mm";
                var guanCai = "未填写";
                var guanDaoLeiXing = "WS污水管道";
                var jianCeDanWei = "苏州蛟视管道检测技术有限公司";
                var jianCeYuan = "Alex";
                var task =
                    $"任务名称：{renWu}\n" +
                    $"检测方向：{fangXiang}\n" +
                    $"检测地点：{diDian}\n" +
                    $"检测日期：{riQi}\n" +
                    $"起始井号：{qiShiJingHao}\n" +
                    $"终止井号：{zhongZhiJingHao}\n" +
                    $"管        径：{guanJing}\n" +
                    $"管        材：{guanCai}\n" +
                    $"管道类型：{guanDaoLeiXing}\n" +
                    $"检测单位：{jianCeDanWei}\n" +
                    $"检测    员：{jianCeYuan}";
                _videoOverlayInfo["text"] = task;
                _fileOverlayInfo["text"] = task;

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(20)
                };
                timer.Start();
                timer.Tick += (_, _) =>
                {
                    var date = GetDateString();
                    _videoOverlayInfo["text"] = $"{date} {qiShiJingHao}-{zhongZhiJingHao} ({fangXiang})";
                    _fileOverlayInfo["text"] = $"{date} {qiShiJingHao}-{zhongZhiJingHao} ({fangXiang})";

                    timer.Stop();
                    timer = null;
                };
            });
        }

        private void ButtonDate_OnClick(object sender, RoutedEventArgs e)
        {
            var date = GetDateString();
            _videoOverlayInfo["text"] = date;
            _fileOverlayInfo["text"] = date;
        }

        private static string GetDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void ButtonRecordStart_OnClick(object sender, RoutedEventArgs e)
        {
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Null);

            _fileSink["location"] = FileUtil.GetRecordFilename();
            if (!Element.Link(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink))
            {
                Log.E("Elements could not be linked");
                return;
            }

            _queueFilePad = _fileQueue.GetStaticPad("sink");
            if (_teeFilePad.Link(_queueFilePad) != PadLinkReturn.Ok)
            {
                MessageBox.Show("Failed to Link Video Pad.");
                return;
            }

            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Playing);

            ButtonRecordStop.IsEnabled = true;
            ButtonRecordStart.IsEnabled = false;
        }

        private void ButtonRecordStop_OnClick(object sender, RoutedEventArgs e)
        {
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Null);
            if (!_teeFilePad.Unlink(_queueFilePad))
            {
                MessageBox.Show("Failed to Unlink Video Pad.");
                return;
            }

            Element.Unlink(_fileQueue, _fileOverlayClock, _fileOverlayInfo, _fileConvert, _fileEncode, _fileMux,
                _fileSink);
            //加入这句话可以把缓存输出到文件，不知道原因
            _fileSink["location"] = FileUtil.GetRecordFilename();

            _pipeline.SetState(State.Ready);
            _pipeline.SetState(State.Paused);
            _pipeline.SetState(State.Playing);

            ButtonRecordStop.IsEnabled = false;
            ButtonRecordStart.IsEnabled = true;
        }
    }
}