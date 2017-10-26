﻿//--------------------------------------------------------------------------------------
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
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using MySql.Data.MySqlClient;
using System.Collections;

namespace FaceID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        string UserRegister = System.Configuration.ConfigurationManager.AppSettings["UserRegister"];
        private Thread processingThread;
        private PXCMSenseManager senseManager;
        private PXCMFaceConfiguration.RecognitionConfiguration recognitionConfig;
        private PXCMFaceData faceData;
        private PXCMFaceData.RecognitionData recognitionData;
        private Int32 numFacesDetected;
        private string userId, FaceAttributes;
        private string dbState;
        private const int DatabaseUsers = 10;
        private const string DatabaseName = "UserDB";
        private const string DatabaseFilename = "database.bin";
   
        private int faceRectangleHeight;
        private int faceRectangleWidth;
        private int faceRectangleX;
        private int faceRectangleY;
        Hashtable ht = new Hashtable();

        public  MainWindow()
        {
            InitializeComponent();
            rectFaceMarker.Visibility = Visibility.Hidden;
            numFacesDetected = 0;
            userId = string.Empty;
            dbState = string.Empty;
            // Start SenseManage and configure the face module
            ConfigureRealSense();
            // Start the worker thread
            processingThread = new Thread(new ThreadStart(ProcessingThread));
            processingThread.Start();
        }

        private  void ConfigureRealSense()
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
            senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode(PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);
            // Release resources
            faceConfig.Dispose();
            faceModule.Dispose();
         }

        private  void ProcessingThread()
        {
            // Start AcquireFrame/ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
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
                            if (!Directory.Exists(UserRegister + "Images\\"))//如果不存在就创建file文件夹　　             　　                
                                Directory.CreateDirectory(UserRegister + "Images\\");//创建该文件夹　
                            string filefullname = UserRegister + "Images\\" + string.Format("{0}.jpg", System.Guid.NewGuid().ToString());
                            if (!File.Exists(filefullname))
                            {
                                colorBitmap.Save(filefullname, System.Drawing.Imaging.ImageFormat.Jpeg);
                            }

                            userId = recognitionData.QueryUserID().ToString();

                            // 多人以后也需要监视
                            //if (faces > 1)
                            //{
                            //    string filename = UserRegister + string.Format("{0}.txt", 2);
                            //    Log log = new Log(filename);
                            //    log.log(string.Format("检测摄像头前有人数:" + faces + "人，不支持人脸支付！"));
                            //    Environment.Exit(0);
                            //    return; personGroupID = "", personId = "";
                            //}
                            lock (this)
                            {
                                if (!ht.ContainsKey(userId)) {
                                    ht.Add(userId, filefullname);
                                    ProcessIMAGES(userId,filefullname);
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
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                sample.color.Dispose();
                // Release the frame
                senseManager.ReleaseFrame();
            }
        }
      

        async void ProcessIMAGES(string userId,string imageFilePath)
        {
            try
            {
                var client = new HttpClient();
                // Request headers
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "575223f6ffda4f03b73dc9c8a5cc4a29");
                string queryString = "returnFaceId=true&returnFaceAttributes=age,gender,headPose,smile,facialHair,glasses,emotion,hair,makeup,occlusion,accessories,blur,exposure,noise";
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
                            // 多用户以后处理
                            return;
                        }
                        else
                        {
                            JObject joResponse = JObject.Parse(array[0].ToString());
                            JObject ojObject = (JObject)joResponse["faceAttributes"];

                            string faceID = joResponse["faceId"].ToString();
                            string gender = ojObject["gender"].ToString();
                            string age = ojObject["age"].ToString();
                            string smile = ojObject["smile"].ToString();
                            string glasses = ojObject["glasses"].ToString();
                            JObject emotion = (JObject)ojObject["emotion"];
                            string anger = emotion["anger"].ToString();
                            string contempt = emotion["contempt"].ToString();
                            string disgust = emotion["disgust"].ToString();
                            string fear = emotion["fear"].ToString();
                            string happiness = emotion["happiness"].ToString();
                            string neutral = emotion["neutral"].ToString();
                            string sadness = emotion["sadness"].ToString();
                            string surprise = emotion["surprise"].ToString();

                            FaceAttributes = "性别：" + gender + System.Environment.NewLine +
                                             "年龄：" + age + System.Environment.NewLine +
                                             "微笑：" + smile + System.Environment.NewLine +
                                             "眼镜：" + glasses + System.Environment.NewLine +
                                             "愤怒：" + anger + System.Environment.NewLine +
                                             "鄙视：" + contempt + System.Environment.NewLine +
                                             "厌恶：" + disgust + System.Environment.NewLine +
                                             "恐惧：" + fear + System.Environment.NewLine +
                                             "幸福：" + happiness + System.Environment.NewLine +
                                             "中性：" + neutral + System.Environment.NewLine +
                                             "悲伤：" + sadness + System.Environment.NewLine +
                                             "惊讶：" + surprise + System.Environment.NewLine;

                     
                            string  connStr = System.Configuration.ConfigurationManager.ConnectionStrings["mysqlcon"].ToString();
                            MySqlConnection conn = new MySqlConnection(connStr);
                         
                            MySqlParameter[] parameters = {
                                new MySqlParameter("@faceID", faceID), new MySqlParameter("@gender", gender),
                                new MySqlParameter("@age", age), new MySqlParameter("@smile", smile),
                                new MySqlParameter("@glasses", glasses), new MySqlParameter("@anger", anger),
                                new MySqlParameter("@contempt", contempt), new MySqlParameter("@disgust", disgust),
                                new MySqlParameter("@fear", fear), new MySqlParameter("@happiness", happiness),
                                new MySqlParameter("@neutral", neutral), new MySqlParameter("@sadness", sadness),
                                new MySqlParameter("@surprise", surprise), new MySqlParameter("@faceimage", imageFilePath)
                            };

                            try
                            {
                                conn.Open();
                                if (MySqlHelper.ExecuteNonQuery(conn,
                                " insert into coodellshop_faceIdentity(faceid,gender,age,smile,glasses,anger,contempt,disgust,fear,happiness,neutral,sadness,surprise,faceimage) " +
                                " values(@faceid,@gender,@age,@smile,@glasses,@anger,@contempt,@disgust,@fear,@happiness,@neutral,@sadness,@surprise,@faceimage)",
                                parameters) > 0)
                                {
                                    return;
                                }
                                conn.Close();
                            }
                            catch (Exception ex)
                            {
                                conn.Close();
                                MessageBox.Show(ex.Message.ToString());
                                throw;
                            }
                            
                 

                        }
                    }
                }
                ht.Remove(userId);
            }
            catch (Exception ex)
            {
                Thread.Sleep(3000);
                return;
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
                if ((numFacesDetected > 0) )
                {
                    // Show face marker
                    rectFaceMarker.Height = faceRectangleHeight;
                    rectFaceMarker.Width = faceRectangleWidth;
                    Canvas.SetLeft(rectFaceMarker, faceRectangleX);
                    Canvas.SetTop(rectFaceMarker, faceRectangleY);
                    rectFaceMarker.Visibility = Visibility.Visible;

                
                    PXCMFaceData.Face face = faceData.QueryFaceByIndex(0);
                    recognitionData = face.QueryRecognition();

                    // Show floating ID label
                    lblFloatingId.Content = String.Format("{0}", FaceAttributes);
                    Canvas.SetLeft(lblFloatingId, faceRectangleX + faceRectangleWidth);
                    Canvas.SetTop(lblFloatingId, faceRectangleY);
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
            bitmap.Dispose();
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



      
    }
}
