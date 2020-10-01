using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DirectShowLib;


namespace TestTask2
{
    public partial class Form1 : Form
    {
        static void checkHR(int hr, string msg)
        {
            if (hr < 0)
            {
                MessageBox.Show(msg);
                DsError.ThrowExceptionForHR(hr);

            }
        }
        static void BuildGraph(IGraphBuilder pGraph)
        {
            int hr = 0;

            ICaptureGraphBuilder2 pBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            hr = pBuilder.SetFiltergraph(pGraph);
            checkHR(hr, "Can't SetFilterGraph");
            Guid CLSID_WDMStreamingCaptureDevice = new Guid("{65E8773D-8F56-11D0-A3B9-00A0C9223196}");
            Guid CLSID_SampleGrabber = new Guid("{C1F400A0-3F08-11D3-9F0B-006008039E37}");
            Guid CLSID_VidoeRenderer = new Guid("{B87BEB7B-8D29-423F-AE4D-6582C10175AC}");

            //Инициализация Веб-камеры@"EasyCamera"
            
            IBaseFilter pEasyCamera = CreateFilterByName(@"EasyCamera", CLSID_WDMStreamingCaptureDevice);
            hr = pGraph.AddFilter(pEasyCamera, "EasyCamera");
            checkHR(hr, "Cant add EasyCamera at graph");

            //Инициализация филтра Smart Tee
            IBaseFilter pSmartTee = (IBaseFilter)new SmartTee();
            hr = pGraph.AddFilter(pSmartTee, "Smart Tee");
            checkHR(hr, "Can't Add Smart Tee");

            //Инициализация SampleGrabber
            IBaseFilter pSampleGrabber = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_SampleGrabber));
            hr = pGraph.AddFilter(pSampleGrabber, "SampleGrabber");
            checkHR(hr, "Can't Add SampleGrabber");
            AMMediaType pSampleGrabber_pmt = new AMMediaType();
            pSampleGrabber_pmt.majorType = MediaType.Video;
            pSampleGrabber_pmt.subType = MediaSubType.MJPG;
            pSampleGrabber_pmt.formatType = FormatType.VideoInfo;
            pSampleGrabber_pmt.fixedSizeSamples = true;
            pSampleGrabber_pmt.formatSize = 88;
            pSampleGrabber_pmt.sampleSize = 2764800;
            pSampleGrabber_pmt.temporalCompression = false;
            VideoInfoHeader p_sampleGrabber_format = new VideoInfoHeader();
            p_sampleGrabber_format.SrcRect = new DsRect();
            p_sampleGrabber_format.TargetRect = new DsRect();
            p_sampleGrabber_format.BitRate = 442368000;
            p_sampleGrabber_format.AvgTimePerFrame = 333333;
            p_sampleGrabber_format.BmiHeader = new BitmapInfoHeader();
            p_sampleGrabber_format.BmiHeader.Size = 40;
            p_sampleGrabber_format.BmiHeader.Width = 640;
            p_sampleGrabber_format.BmiHeader.Height = 640;
            p_sampleGrabber_format.BmiHeader.Planes = 1;
            p_sampleGrabber_format.BmiHeader.BitCount = 24;
            p_sampleGrabber_format.BmiHeader.Compression = 1996444237;
            p_sampleGrabber_format.BmiHeader.ImageSize = 2764800;
            pSampleGrabber_pmt.formatPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(p_sampleGrabber_format));
            Marshal.StructureToPtr(p_sampleGrabber_format, pSampleGrabber_pmt.formatPtr, false);

            hr = ((ISampleGrabber)pSampleGrabber).SetMediaType(pSampleGrabber_pmt);
            DsUtils.FreeAMMediaType(pSampleGrabber_pmt);
            checkHR(hr, "Can't mediatype");


            IBaseFilter pMJPEGDecompressor = (IBaseFilter)new MjpegDec();
            hr = pGraph.AddFilter(pMJPEGDecompressor, "MJPEG Decompressor");

            IBaseFilter pColorSpaceConverter = (IBaseFilter)new Colour();
            hr = pGraph.AddFilter(pColorSpaceConverter, "Color Space Converter");

            IBaseFilter pVideoRenderer = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_VidoeRenderer));
            hr = pGraph.AddFilter(pVideoRenderer, "Video Renderer");


            //Соединение Графа,получение Pin через готовый метод PinDirection
            hr = pGraph.ConnectDirect(DirectShowLib.DsFindPin.ByDirection(pEasyCamera, PinDirection.Output, 0),
                DirectShowLib.DsFindPin.ByDirection(pSmartTee, PinDirection.Input, 0), null);


            hr = pGraph.ConnectDirect(DirectShowLib.DsFindPin.ByDirection(pSmartTee, PinDirection.Output, 0),
                DirectShowLib.DsFindPin.ByDirection(pSampleGrabber, PinDirection.Input, 0), null);

            hr = pGraph.ConnectDirect(DirectShowLib.DsFindPin.ByDirection(pSampleGrabber, PinDirection.Output, 0),
                DirectShowLib.DsFindPin.ByDirection(pMJPEGDecompressor, PinDirection.Input, 0), null);

            hr = pGraph.ConnectDirect(DirectShowLib.DsFindPin.ByDirection(pMJPEGDecompressor, PinDirection.Output, 0),
                DirectShowLib.DsFindPin.ByDirection(pColorSpaceConverter, PinDirection.Input, 0), null);

            hr = pGraph.ConnectDirect(DirectShowLib.DsFindPin.ByDirection(pColorSpaceConverter, PinDirection.Output, 0),
                DirectShowLib.DsFindPin.ByDirection(pVideoRenderer, PinDirection.Input, 0), null);

        }
        public Form1()
        {
            InitializeComponent();

        }




        public static IBaseFilter CreateFilterByName(string filterName, Guid category)
        {
            int hr = 0;
            DsDevice[] devices = DsDevice.GetDevicesOfCat(category);
            foreach (DsDevice dev in devices)
            {
                if (dev.Name == filterName)
                {
                    IBaseFilter filter = null;
                    IBindCtx bindCtx = null;
                    try
                    {
                        hr = CreateBindCtx(0, out bindCtx);
                        DsError.ThrowExceptionForHR(hr);
                        Guid guid = typeof(IBaseFilter).GUID;
                        object obj;
                        dev.Mon.BindToObject(bindCtx, null, ref guid, out obj);
                        filter = (IBaseFilter)obj;
                    }
                    finally
                    {
                        if (bindCtx != null)
                            Marshal.ReleaseComObject(bindCtx);
                    }
                    return filter;
                }

            }
            return null;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                IGraphBuilder graph = (IGraphBuilder)new FilterGraph();
                BuildGraph(graph);
                IMediaControl mediaControl = (IMediaControl)graph;
                IMediaEvent mediaEvent = (IMediaEvent)graph;
                int hr = mediaControl.Run();
                bool stop = false;
                while (!stop)
                {
                    System.Threading.Thread.Sleep(500);
                    EventCode ev;
                    IntPtr p1, p2;
                    System.Windows.Forms.Application.DoEvents();
                    while (mediaEvent.GetEvent(out ev, out p1, out p2, 0) == 0)
                    {
                        if (ev == EventCode.Complete || ev == EventCode.UserAbort)
                        {
                            stop = true;
                        }
                        else
                        if (ev == EventCode.ErrorAbort)
                        {
                            mediaControl.Stop();
                            stop = true;
                        }
                        mediaEvent.FreeEventParams(ev, p1, p2);


                    }
                }
            }
            catch (COMException ex)
            {
                MessageBox.Show("COM Error" + ex.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.ToString());
            }
        }

        [DllImport("ole32.dll")]
        public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);


    }
}
