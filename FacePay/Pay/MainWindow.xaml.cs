//--------------------------------------------------------------------------------------
// Copyright 2015 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.
//--------------------------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Drawing;
using System.Windows.Controls;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace FaceID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        string UserPay = System.Configuration.ConfigurationManager.AppSettings["FacePay"];

        string PhoneFilename = System.Configuration.ConfigurationManager.AppSettings["phonefilename"];

        private Thread processingThread;
        private PXCMSenseManager senseManager;
        private PXCMFaceConfiguration.RecognitionConfiguration recognitionConfig;
        private PXCMFaceData faceData;
        private PXCMFaceData.RecognitionData recognitionData;
        private Int32 numFacesDetected;
        private string userId;
        private string dbState;
        private const int DatabaseUsers = 10;
        private const string DatabaseName = "UserDB";
        private const string DatabaseFilename = "database.bin";
        private int faceRectangleHeight;
        private int faceRectangleWidth;
        private int faceRectangleX;
        private int faceRectangleY;
        public string faceListId, phonenumber, newfaceID="", filefullname = "";

        string useimages="",successlog = "";
        int faces = 0, count = 0;

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        public MainWindow()
        {
            InitializeComponent();
            // 判断目录下是否有手机号文件,如果未发现，生成日志，调用失败
            if (File.Exists(UserPay + PhoneFilename))
            {
                // 读文本文件
                string[] lines = System.IO.File.ReadAllLines(UserPay + PhoneFilename);
                if (lines.Length == 2)
                {
                    phonenumber = lines[0];
                    faceListId = lines[1];
                }
                else
                {
                    string filename = UserPay  + string.Format("{0}.txt", 0);
                    Log log = new Log(filename);
                    log.log("数据不完整，启动摄像头失败!");
                    Environment.Exit(0);
                }
             
            }
            else
            {
                string filename = UserPay + string.Format("{0}.txt", 0);
                Log log = new Log(filename);
                log.log(UserPay + PhoneFilename + "文件不存在,启动摄像头失败");
                Environment.Exit(0);
            }

            rectFaceMarker.Visibility = Visibility.Hidden;
            numFacesDetected = 0;
            userId = string.Empty;
            dbState = string.Empty;
          


            if (!Directory.Exists(UserPay))
            {
                // 清空文件夹
                Directory.CreateDirectory(UserPay);
            }

            // 第一次启动时候删除所有日志信息
            string pattern = "*.txt";
            string[] strFileName = Directory.GetFiles(UserPay, pattern);
            foreach (var item in strFileName)
            {
                File.Delete(item);
            }

            if (Directory.Exists(UserPay + "\\Images\\"))
            {
                // 删除图片文件
                Directory.Delete(UserPay + "\\Images\\", true);
            }

            // Start SenseManage and configure the face module
            ConfigureRealSense();
            // Start the worker thread
            processingThread = new Thread(new ThreadStart(ProcessingThread));
            processingThread.Start();

            
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }
        private void ConfigureRealSense()
        {
            PXCMFaceModule faceModule;
            PXCMFaceConfiguration faceConfig;
            
            // Start the SenseManager and session  
            senseManager = PXCMSenseManager.CreateInstance();

            // Enable the color stream
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);

            // Enable the face module
            senseManager.EnableFace();
            faceModule = senseManager.QueryFace();
            faceConfig = faceModule.CreateActiveConfiguration();

            // Configure for 3D face tracking (if camera cannot support depth it will revert to 2D tracking)
            faceConfig.SetTrackingMode(PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH);

            // Enable facial recognition
            recognitionConfig = faceConfig.QueryRecognition();
            recognitionConfig.Enable();

            // Create a recognition database
            // 创建人脸数据库(根据用户人脸图片生成GUID在数据库)
            PXCMFaceConfiguration.RecognitionConfiguration.RecognitionStorageDesc recognitionDesc = new PXCMFaceConfiguration.RecognitionConfiguration.RecognitionStorageDesc();
            recognitionDesc.maxUsers = DatabaseUsers;
            //recognitionConfig.CreateStorage(DatabaseName, out recognitionDesc);
            //recognitionConfig.UseStorage(DatabaseName);
            LoadDatabaseFromFile();
            recognitionConfig.SetRegistrationMode(PXCMFaceConfiguration.RecognitionConfiguration.RecognitionRegistrationMode.REGISTRATION_MODE_CONTINUOUS);

            // Apply changes and initialize
            faceConfig.ApplyChanges();
            senseManager.Init();
            faceData = faceModule.CreateOutput();

            // Mirror image
            try
            {
                senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode(PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);
            }
            catch (Exception ex)
            {
                string filename = UserPay+ string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd"));
                Log log = new Log(filename);
                log.log(ex.Message);
                Environment.Exit(0);
            }
          

            // Release resources
            faceConfig.Dispose();
            faceModule.Dispose();
         }

        private  void ProcessingThread()
        {
           

            // Start AcquireFrame/ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {


                if (newfaceID != "")
                {
                    VerifyRequest(newfaceID, faceListId);

                    //写日志信息；
                    string successfilename = UserPay + "\\" + string.Format("{0}.txt", 1);
                    string networkfilename = UserPay + "\\" + string.Format("{0}.txt", 4);

                    if (count > 200)
                    {
                        Log log = new Log(networkfilename);
                        log.log("网络异常请重试" + count.ToString());
                        Environment.Exit(0);
                        return;
                    }

                    if (successlog != "")
                    {
                        Log log = new Log(successfilename);
                        log.log(successlog);
                        Environment.Exit(0);
                        return;
                    }
                }


                // Acquire the color image data
                PXCMCapture.Sample sample = senseManager.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);
                
                // Get face data
                if (faceData != null)
                {
                    faceData.Update();
                    numFacesDetected = faceData.QueryNumberOfDetectedFaces();

                    if (numFacesDetected > 0)
                    {
                        // Get the first face detected (index 0)
                        PXCMFaceData.Face face = faceData.QueryFaceByIndex(0);

                        // Retrieve face location data
                        PXCMFaceData.DetectionData faceDetectionData = face.QueryDetection();
                        if (faceDetectionData != null)
                        {
                            PXCMRectI32 faceRectangle;
                            faceDetectionData.QueryBoundingRect(out faceRectangle);
                            faceRectangleHeight = faceRectangle.h;
                            faceRectangleWidth = faceRectangle.w;
                            faceRectangleX = faceRectangle.x;
                            faceRectangleY = faceRectangle.y;
                        }

                        // Process face recognition data
                        if (face != null)
                        {
                            // Retrieve the recognition data instance
                            recognitionData = face.QueryRecognition();
                            // 请求微软接口,计算用户照片是否可用
                            if (!Directory.Exists(UserPay + "\\Images\\"))//如果不存在就创建file文件夹　　             　　                
                                Directory.CreateDirectory(UserPay + "\\Images\\");//创建该文件夹　
                                                                                  //string imagefilename = System.Guid.NewGuid().ToString(); face.QueryUserID().ToString(CultureInfo.InvariantCulture)
                            filefullname = UserPay + "\\Images\\" + string.Format("{0}.jpg",
                                System.Guid.NewGuid().ToString());

                            if (!File.Exists(filefullname))
                            {
                                colorBitmap.Save(filefullname, System.Drawing.Imaging.ImageFormat.Jpeg);
                            }

                            if (faces > 1)
                            {
                                string filename = UserPay + string.Format("{0}.txt", 2);
                                Log log = new Log(filename);
                                log.log(string.Format("检测摄像头前有人数 RealSense:" + faces + "人，不支持人脸支付！"));
                                Environment.Exit(0);
                                return;
                            }

                            lock (this)
                            {
                                if (newfaceID == "")
                                {
                                    ProcessIMAGES(filefullname);
                                }
                              
                            }



                          


                        }
                    }
                    else
                    {
                        userId = "No users in view";
                    }
                }

                // Display the color stream and other UI elements
                UpdateUI(colorBitmap);

                // Release resources
                if (colorBitmap != null)
                {
                    colorBitmap.Dispose();
                }
              
                sample.color.ReleaseAccess(colorData);
                sample.color.Dispose();
                // Release the frame
                senseManager.ReleaseFrame();
            }
        }
        

        private List<string> SetUpImagesList()
        {
            List<string> images = new List<string>();

            string pattern = "*.jpg";
            string[] strFileName = Directory.GetFiles(UserPay + "\\Images\\", pattern);
            foreach (var item in strFileName)
            {
                images.Add(item);
            }

            return images;
        }

        async void ProcessIMAGES(string imageFilePath)
        {

            count = count + 1;
            var client = new HttpClient();
            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "575223f6ffda4f03b73dc9c8a5cc4a29");
            string queryString = "returnFaceId=true";// string.Format("userData={0}&targetFace={1}", "sample_list2", "2222222");

            var uri = string.Format("https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect?") + queryString;
            HttpResponseMessage response;
            string responseContent;
            // Request body
            byte[] byteData = GetImageAsByteArray(imageFilePath);
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(uri, content);
                responseContent = response.Content.ReadAsStringAsync().Result;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (responseContent != "[]")
                {
                    JArray array = JArray.Parse(responseContent);
                    if (array.Count > 1)
                    {
                        faces = array.Count;
                        return;
                    }
                    else
                    {
                        JObject joResponse = JObject.Parse(array[0].ToString());
                        newfaceID = joResponse["faceId"].ToString(); //计算比较用
                        useimages = imageFilePath;
                    }

                }
              
            }

        }




        public async void VerifyRequest(string newfaceid, string FaceListId)
        {


            count = count + 1;
            var client = new HttpClient();
            string queryString = string.Format("faceId={0}&faceListId={1}&maxNumOfCandidatesReturned={2}&mode={3}",
                newfaceid, FaceListId,
                20,
                "matchFace");
            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "575223f6ffda4f03b73dc9c8a5cc4a29");
            var uri = string.Format("https://southeastasia.api.cognitive.microsoft.com/face/v1.0/findsimilars?") + queryString;
            HttpResponseMessage response;
            string responseContent;
            // Request body

            //string value = "{\"faceId\":\"" + newfaceid + "\",\"faceListId\":\"" + FaceListId + "\",\"maxNumOfCandidatesReturned\":\"20\",\"mode\":\"matchFace\"}";
            string value = "{\"faceId\":\""+ newfaceid + "\",\"faceListId\":\""+ FaceListId + "\",\"maxNumOfCandidatesReturned\":\"20\",\"mode\":\"matchFace\"}";

            byte[] byteData = Encoding.UTF8.GetBytes(value);

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                responseContent = response.Content.ReadAsStringAsync().Result;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                responseContent = responseContent.Replace("[", "").Replace("]", "");
                JObject joResponse = JObject.Parse(responseContent);


                FileInfo fi = new FileInfo(useimages);
                if (fi.Exists)
                {
                    if (!Directory.Exists(UserPay + "\\FacePayImages\\"))//如果不存在就创建file文件夹　　             　　                
                        Directory.CreateDirectory(UserPay + "\\FacePayImages\\");//创建该文件夹　
                    fi.MoveTo(UserPay + "\\FacePayImages\\" + newfaceid.ToString() + ".jpg");
                }


                string isIdentical;
                if (double.Parse(joResponse["confidence"].ToString()) < double.Parse("0.5"))
                {
                    isIdentical = "False";
                }
                else
                {
                    isIdentical = "True";


                }
                successlog = "";
                successlog = successlog + Environment.NewLine + phonenumber;
                successlog = successlog + Environment.NewLine + "isIdentical:" + isIdentical;
                successlog = successlog + Environment.NewLine + "FacePayImages:" + newfaceid + ".jpg";
                successlog = successlog + Environment.NewLine + "persistedFaceId:" + joResponse["persistedFaceId"].ToString();
                successlog = successlog + Environment.NewLine + "confidence:" + joResponse["confidence"].ToString();
            }



        }


        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }

        private void UpdateUI(Bitmap bitmap)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate()
            {
                // Display  the color image
                if (bitmap != null)
                {
                    imgColorStream.Source = ConvertBitmap.BitmapToBitmapSource(bitmap);
                }

              

                // Change picture border color depending on if user is in camera view
                if (numFacesDetected > 0)
                {
                    bdrPictureBorder.BorderBrush = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    bdrPictureBorder.BorderBrush = System.Windows.Media.Brushes.Red;
                }

                // Show or hide face marker
                if ((numFacesDetected > 0))
                {
                    // Show face marker
                    rectFaceMarker.Height = faceRectangleHeight;
                    rectFaceMarker.Width = faceRectangleWidth;
                    Canvas.SetLeft(rectFaceMarker, faceRectangleX);
                    Canvas.SetTop(rectFaceMarker, faceRectangleY);
                    rectFaceMarker.Visibility = Visibility.Visible;

                    // Show floating ID label
                    lblFloatingId.Content = String.Format("人脸支付中...", userId);
                    Canvas.SetLeft(lblFloatingId, faceRectangleX);
                    Canvas.SetTop(lblFloatingId, faceRectangleY - 20);
                    lblFloatingId.Visibility = Visibility.Visible;
                }
                else
                {
                    // Hide the face marker and floating ID label
                    rectFaceMarker.Visibility = Visibility.Hidden;
                    lblFloatingId.Visibility = Visibility.Hidden;
                }
            }));

            // Release resources
            if (bitmap != null)
            {
                bitmap.Dispose();
            }
               
        }

        private void LoadDatabaseFromFile()
        {
            if (File.Exists(DatabaseFilename))
            {
                Byte[] buffer = File.ReadAllBytes(DatabaseFilename);
                recognitionConfig.SetDatabaseBuffer(buffer);
                dbState = "Loaded";
            }
            else
            {
                dbState = "Not Found";
            }
        }

        private void SaveDatabaseToFile()
        {
            // Allocate the buffer to save the database
            PXCMFaceData.RecognitionModuleData recognitionModuleData = faceData.QueryRecognitionModule();
            Int32 nBytes = recognitionModuleData.QueryDatabaseSize();
            Byte[] buffer = new Byte[nBytes];

            // Retrieve the database buffer
            recognitionModuleData.QueryDatabaseBuffer(buffer);

            // Save the buffer to a file
            // (NOTE: production software should use file encryption for privacy protection)
            File.WriteAllBytes(DatabaseFilename, buffer);
            dbState = "Saved";
        }

        private void DeleteDatabaseFile()
        {
            if (File.Exists(DatabaseFilename))
            {
                File.Delete(DatabaseFilename);
                dbState = "Deleted";
            }
            else
            {
                dbState = "Not Found";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            // 设置全屏  
            this.WindowState = System.Windows.WindowState.Normal;
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;

            this.Left = 0.0;
            this.Top = 0.0;
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;
        }

        private void ReleaseResources()
        {
            // Stop the worker thread
            processingThread.Abort();

            // Release resources
            faceData.Dispose();
            senseManager.Dispose();
        }



        private void btnSaveDatabase_Click(object sender, RoutedEventArgs e)
        {
            SaveDatabaseToFile();
        }

        private void btnDeleteDatabase_Click(object sender, RoutedEventArgs e)
        {
            DeleteDatabaseFile();
        }
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            ReleaseResources();
            this.Close();
            
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ReleaseResources();
        }
    }
}
