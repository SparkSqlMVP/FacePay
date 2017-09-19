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
        public string faceid, phonenumber, newfaceID="", filefullname = "";

        string errorlog = "", successlog = "";
        int flag = 0;
    
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
                    faceid = lines[1];
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
            chkShowFaceMarker.IsChecked = true;
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

                //写日志信息；
                string errorfilename = UserPay + "\\" + string.Format("{0}.txt", 0);
                string successfilename = UserPay + "\\" + string.Format("{0}.txt", 1);
                if (newfaceID != "")
                {
                        VerifyRequest(filefullname, faceid, newfaceID);
                        if (errorlog != "")
                        {
                            Log log = new Log(errorfilename);
                            log.log(errorlog);
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
                            filefullname = UserPay + "\\Images\\" + string.Format("{0}.jpg", face.QueryUserID().ToString(CultureInfo.InvariantCulture));

                            if (!File.Exists(filefullname))
                            {
                                colorBitmap.Save(filefullname, System.Drawing.Imaging.ImageFormat.Jpeg);
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
        
            try
            {

                var client = new HttpClient();
                // Request headers - replace this example key with your valid key.
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "09dfdeb77fee410ba27ebc807ffd4ec8");

                // Request parameters.
                string queryString = "returnFaceId=true";

                // NOTE: You must use the same region in your REST call as you used to obtain your subscription keys.
                //   For example, if you obtained your subscription keys from westus, replace "westcentralus" in the 
                //   URI below with "westus".
                string uri = "https://westus.api.cognitive.microsoft.com/face/v1.0/detect?" + queryString;

                HttpResponseMessage response;
                string responseContent;

                // Request body. Try this sample with a locally stored JPEG image.
                byte[] byteData = GetImageAsByteArray(imageFilePath);

                using (var content = new ByteArrayContent(byteData))
                {
                    // This example uses content type "application/octet-stream".
                    // The other content types you can use are "application/json" and "multipart/form-data".
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response = await client.PostAsync(uri, content);
                    responseContent = response.Content.ReadAsStringAsync().Result;
                }

                //A peak at the JSON response.
                //Console.WriteLine(responseContent);
            
                if (responseContent == "[]")
                {

                    // 此位置识别不到您
                    try
                    {
                        //System.IO.File.Delete(imageFilePath);
                       // errorlog = errorlog + Environment.NewLine + string.Format("照片:{0},采集的脸部特征无效,需要重新采集!", imageFilePath);
                    }
                    catch (Exception ex)
                    {
                        //string filename = UserPay + string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd"));
                        //Log log = new Log(filename);
                        //log.log(ex.Message.ToString());
                    }


                }
                else
                {
                    try
                    {
                        JArray array = JArray.Parse(responseContent);

                        if (array.Count > 1)
                        {
                            //errorlog = errorlog + Environment.NewLine + string.Format("检测摄像头前有人数:" + array.Count + "人，不支持人脸支付！");
                            return;
                        }
                        else
                        {
                            JObject joResponse = JObject.Parse(array[0].ToString());
                            newfaceID = joResponse["faceId"].ToString(); //计算比较用
                        }
                     
                    }
                    catch (Exception ex)
                    {
                        // Rate limit is exceeded. Try again later.
                        //string filename = UserPay + string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd") );
                        //Log log = new Log(filename);
                        //log.log(ex.Message.ToString());
                    }

                  
                }

            }
            catch (Exception ex)
            {
                // 网络原因，不能连接接口

                //string filename = UserPay+ string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd"));
                //Log log = new Log(filename);
                //log.log(ex.Message.ToString());

            }


        }




        public async void VerifyRequest(string imageFilePath, string faceID, string newfaceID)
        {
            try
            {

                var client = new HttpClient();
                // Request headers - replace this example key with your valid key.
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "09dfdeb77fee410ba27ebc807ffd4ec8");

                // Request parameters.
                string queryString = string.Format("faceId1={0}&faceId2={1}", faceID, newfaceID);

                // NOTE: You must use the same region in your REST call as you used to obtain your subscription keys.
                //   For example, if you obtained your subscription keys from westus, replace "westcentralus" in the 
                //   URI below with "westus".
                string uri = "https://westus.api.cognitive.microsoft.com/face/v1.0/verify?" + queryString;
                HttpResponseMessage response;
                string responseContent;

                
                // Request body
                byte[] byteData = Encoding.UTF8.GetBytes("{\"faceId1\":\""+ faceID + "\",\"faceId2\":\""+ newfaceID + "\"}");

                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    response = await client.PostAsync(uri, content);
                    responseContent = response.Content.ReadAsStringAsync().Result;
                }

                //A peak at the JSON response.
                if (responseContent == "[]")
                {

                    // 此位置识别不到您
                    try
                    {
                        errorlog = errorlog + Environment.NewLine + string.Format("刷脸支付：暂时不支持刷年支付，请联系管理员!", imageFilePath);
                    }
                    catch (Exception ex)
                    {
                        //string filename = UserPay + string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd"));
                        //Log log = new Log(filename);
                        //log.log(ex.Message.ToString());
                        //  throw;
                    }


                }
                else
                {
                    try
                    {
                        JObject joResponse = JObject.Parse(responseContent);
                        string isIdentical = joResponse["isIdentical"].ToString();
                        string confidence = joResponse["confidence"].ToString();

                        if (!Directory.Exists(UserPay + "\\FacePayImages\\"))//如果不存在就创建file文件夹　　             　　                
                            Directory.CreateDirectory(UserPay + "\\FacePayImages\\");//创建该文件夹　


                        if (isIdentical.ToUpper() == "TRUE")
                        {
                            // 更新文件名
                            FileInfo fi = new FileInfo(imageFilePath);
                            if (fi.Exists)
                            {

                                fi.MoveTo(UserPay + "\\FacePayImages\\" + newfaceID.ToString() + ".jpg");
                            }

                            successlog = successlog + Environment.NewLine + phonenumber;
                            successlog = successlog + Environment.NewLine + "FacePayImages:" + newfaceID + ".jpg";
                            successlog = successlog + Environment.NewLine + "isIdentical:" + isIdentical;
                            successlog = successlog + Environment.NewLine + "confidence:" + confidence;

                            
                        }
                        else
                        {

                            errorlog = errorlog + Environment.NewLine + phonenumber;
                            errorlog = errorlog + Environment.NewLine + "FacePayImages:" + newfaceID + ".jpg";
                            errorlog = errorlog + Environment.NewLine + "isIdentical:" + isIdentical;
                            errorlog = errorlog + Environment.NewLine + "confidence:" + confidence;
                        }

                    }
                    catch (Exception ex)
                    {
                        // Rate limit is exceeded. Try again later.

                        //string filename = UserPay+ string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd") );
                        //Log log = new Log(filename);
                        //log.log(ex.Message.ToString());

                    }


                }

            }
            catch (Exception ex)
            {
                //// 网络原因，不能连接接口
                //string filename = UserPay + string.Format("{0}.txt", System.DateTime.Now.ToString("yyyyMMdd"));
                //Log log = new Log(filename);
                //log.log(ex.Message.ToString());
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

                 // Update UI elements
                lblNumFacesDetected.Content = String.Format("Faces Detected: {0}", numFacesDetected);
                lblUserId.Content = String.Format("User ID: {0}", userId);
                lblDatabaseState.Content = String.Format("Database: {0}", dbState);

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
                if ((numFacesDetected > 0) && (chkShowFaceMarker.IsChecked == true))
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
