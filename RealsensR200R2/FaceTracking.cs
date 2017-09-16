using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Windows.Forms;

namespace DF_FaceTracking.cs
{
    internal class FaceTracking
    {
        private readonly MainForm m_form;
        private FPSTimer m_timer;
        private bool m_wasConnected;

        public FaceTracking(MainForm form)
        {
            m_form = form;
        }
        
        public void OnFiredAlert(Object sender, Intel.RealSense.Face.FaceConfiguration.AlertEventArgs args)
        {
            Intel.RealSense.Face.AlertData data = args.data;
            string alert = "Alert: ";
            m_form.UpdateStatus(alert + data.label.ToString(), MainForm.Label.StatusLabel);
        }

        private void DisplayDeviceConnection(bool isConnected)
        {
            if (isConnected && !m_wasConnected) m_form.UpdateStatus("Device Reconnected", MainForm.Label.StatusLabel);
            else if (!isConnected && m_wasConnected)
                m_form.UpdateStatus("Device Disconnected", MainForm.Label.StatusLabel);
            m_wasConnected = isConnected;
        }

        private void DisplayPicture(Intel.RealSense.Image image)
        {
            Intel.RealSense.ImageData data;
            if (image.AcquireAccess(Intel.RealSense.ImageAccess.ACCESS_READ, Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB32, out data) <
                Intel.RealSense.Status.STATUS_NO_ERROR) return;
            m_form.DrawBitmap(data.ToBitmap(0, image.Info.width, image.Info.height));
            m_timer.Tick("");
            image.ReleaseAccess(data);
        }

        private void CheckForDepthStream(Intel.RealSense.StreamProfileSet profiles, Intel.RealSense.Face.FaceModule faceModule)
        {
            Intel.RealSense.Face.FaceConfiguration faceConfiguration = faceModule.CreateActiveConfiguration();
            if (faceConfiguration == null)
            {
                Debug.Assert(faceConfiguration != null);
                return;
            }

            Intel.RealSense.Face.TrackingModeType trackingMode = faceConfiguration.TrackingMode;
            faceConfiguration.Dispose();

            if (trackingMode != Intel.RealSense.Face.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH
                || trackingMode != Intel.RealSense.Face.TrackingModeType.FACE_MODE_IR)
                return;
            if (profiles.depth.imageInfo.format == Intel.RealSense.PixelFormat.PIXEL_FORMAT_DEPTH) return;
            Intel.RealSense.DeviceInfo dinfo;
            m_form.Devices.TryGetValue(m_form.GetCheckedDevice(), out dinfo);

            if (dinfo != null)
                MessageBox.Show(
                    String.Format("Depth stream is not supported for device: {0}. \nUsing 2D tracking", dinfo.name),
                    @"Face Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void SimplePipeline()
        {
            Intel.RealSense.SenseManager pp = m_form.Session.CreateSenseManager();

            if (pp == null)
            {
                throw new Exception("PXCMSenseManager null");
            }

            Intel.RealSense.CaptureManager captureMgr = pp.CaptureManager;
            if (captureMgr == null)
            {
                throw new Exception("PXCMCaptureManager null");
            }

            var selectedRes = m_form.GetCheckedColorResolution();
            if (selectedRes != null && !m_form.IsInPlaybackState())  
            {
                // Set active camera
                Intel.RealSense.DeviceInfo deviceInfo;
                m_form.Devices.TryGetValue(m_form.GetCheckedDevice(), out deviceInfo);
                captureMgr.FilterByDeviceInfo(m_form.GetCheckedDeviceInfo());

                // activate filter only live/record mode , no need in playback mode
                var set = new Intel.RealSense.StreamProfileSet
                {
                    color =
                    {
                        frameRate = selectedRes.Item2,
                        imageInfo =
                        {
                            format = selectedRes.Item1.format,
                            height = selectedRes.Item1.height,
                            width = selectedRes.Item1.width
                        }
                    }
                };

                if (m_form.IsPulseEnabled() && (set.color.imageInfo.width < 1280 || set.color.imageInfo.height < 720))
                {
                    captureMgr.FilterByStreamProfiles(Intel.RealSense.StreamType.STREAM_TYPE_COLOR, 1280, 720, 0);
                }
                else
                {
                    captureMgr.FilterByStreamProfiles(set);
                }
            }

            // Set Source & Landmark Profile Index 
            if (m_form.IsInPlaybackState())
            {
                //pp.captureManager.FilterByStreamProfiles(null);
                captureMgr.SetFileName(m_form.GetFileName(), false);
                captureMgr.Realtime = false;
            }
            else  if (m_form.GetRecordState())
            {
                captureMgr.SetFileName(m_form.GetFileName(), true);
            }
            
            // Set Module
            Intel.RealSense.Face.FaceModule faceModule = Intel.RealSense.Face.FaceModule.Activate(pp);
            if (faceModule == null)
            {
                Debug.Assert(faceModule != null);
                return;
            }

            Intel.RealSense.Face.FaceConfiguration moduleConfiguration = faceModule.CreateActiveConfiguration();

            if (moduleConfiguration == null)
            {
                Debug.Assert(moduleConfiguration != null);
                return;
            }

            var checkedProfile = m_form.GetCheckedProfile();
            var mode = m_form.FaceModesMap.First(x => x.Value == checkedProfile).Key;
            
            moduleConfiguration.TrackingMode = mode;

            moduleConfiguration.Strategy = Intel.RealSense.Face.TrackingStrategyType.STRATEGY_RIGHT_TO_LEFT;

            moduleConfiguration.Detection.maxTrackedFaces = m_form.NumDetection;
            moduleConfiguration.Landmarks.maxTrackedFaces = m_form.NumLandmarks;
            moduleConfiguration.Pose.maxTrackedFaces = m_form.NumPose;

            Intel.RealSense.Face.ExpressionsConfiguration econfiguration = moduleConfiguration.Expressions;
            if (econfiguration == null)
            {
                throw new Exception("ExpressionsConfiguration null");
            }
            econfiguration.Properties.maxTrackedFaces = m_form.NumExpressions;

            econfiguration.EnableAllExpressions();
            moduleConfiguration.Detection.isEnabled = m_form.IsDetectionEnabled();
            moduleConfiguration.Landmarks.isEnabled = m_form.IsLandmarksEnabled();
            moduleConfiguration.Pose.isEnabled = m_form.IsPoseEnabled();
            if (m_form.IsExpressionsEnabled())
            {
                econfiguration.Properties.Enabled = true;
            }

            Intel.RealSense.Face.PulseConfiguration pulseConfiguration = moduleConfiguration.Pulse;
            if (pulseConfiguration == null)
            {
                throw new Exception("pulseConfiguration null");
            }
			
            pulseConfiguration.Properties.maxTrackedFaces = m_form.NumPulse;
            if (m_form.IsPulseEnabled())
            {
                pulseConfiguration.Enabled = true;
            }

            Intel.RealSense.Face.RecognitionConfiguration qrecognition = moduleConfiguration.Recognition;
            if (qrecognition == null)
            {
                throw new Exception("PXCMFaceConfiguration.RecognitionConfiguration null");
            }
            if (m_form.IsRecognitionChecked())
            {
                qrecognition.Properties.isEnabled = true;
            }

            moduleConfiguration.EnableAllAlerts();
            moduleConfiguration.AlertFired += OnFiredAlert;

            Intel.RealSense.Status applyChangesStatus = moduleConfiguration.ApplyChanges();

            m_form.UpdateStatus("Init Started", MainForm.Label.StatusLabel);

            if (applyChangesStatus < Intel.RealSense.Status.STATUS_NO_ERROR || pp.Init() < Intel.RealSense.Status.STATUS_NO_ERROR)
            {
                m_form.UpdateStatus("Init Failed", MainForm.Label.StatusLabel);
            }
            else
            {
                using (Intel.RealSense.Face.FaceData moduleOutput = faceModule.CreateOutput())
                {
                    Debug.Assert(moduleOutput != null);
                    Intel.RealSense.StreamProfileSet profiles;
                    Intel.RealSense.Device device  =  captureMgr.Device;

                    if (device == null)
                    {
                        throw new Exception("device null");
                    }
                    
                    device.QueryStreamProfileSet(Intel.RealSense.StreamType.STREAM_TYPE_DEPTH, 0, out profiles);
                    CheckForDepthStream(profiles, faceModule);
                    
                    m_form.UpdateStatus("Streaming", MainForm.Label.StatusLabel);
                    m_timer = new FPSTimer(m_form);

                    while (!m_form.Stopped)
                    {
                        if (pp.AcquireFrame(true) < Intel.RealSense.Status.STATUS_NO_ERROR) break;
                        var isConnected = pp.IsConnected();
                        DisplayDeviceConnection(isConnected);
                        if (isConnected)
                        {
                            var sample = pp.Sample;
                            if (sample == null)
                            {
                                pp.ReleaseFrame();
                                continue;
                            }
                            switch (mode)
                            {
                                case Intel.RealSense.Face.TrackingModeType.FACE_MODE_IR:
                                    if (sample.Ir != null)
                                    DisplayPicture(sample.Ir);
                                    break;
                                default:
                                    DisplayPicture(sample.Color);
                                    break;
                            }

                            moduleOutput.Update();
                            Intel.RealSense.Face.RecognitionConfiguration recognition = moduleConfiguration.Recognition;
                            if (recognition == null)
                            {
                                pp.ReleaseFrame();
                                continue;
                            }

                            if (recognition.Properties.isEnabled)
                            {
                                UpdateRecognition(moduleOutput);
                            }

                            m_form.DrawGraphics(moduleOutput);
                            m_form.UpdatePanel();
                        }
                        pp.ReleaseFrame();
                    }

                }

   //             moduleConfiguration.ApplyChanges();
                m_form.UpdateStatus("Stopped", MainForm.Label.StatusLabel);
            }
            moduleConfiguration.Dispose();
            pp.Close();
            pp.Dispose();
        }

        private void UpdateRecognition(Intel.RealSense.Face.FaceData faceOutput)
        {
            //TODO: add null checks
            if (m_form.Register) RegisterUser(faceOutput);
            if (m_form.Unregister) UnregisterUser(faceOutput);
        }

        private void RegisterUser(Intel.RealSense.Face.FaceData faceOutput)
        {
            m_form.Register = false;
            if (faceOutput.NumberOfDetectedFaces <= 0)
                return;

            Intel.RealSense.Face.Face qface = faceOutput.QueryFaceByIndex(0);
            if (qface == null)
            {
                throw new Exception("PXCMFaceData.Face null");
            }
            Intel.RealSense.Face.RecognitionData rdata = qface.Recognition;
            if (rdata == null)
            {
                throw new Exception(" PXCMFaceData.RecognitionData null");
            }
            rdata.RegisterUser();
        }

        private void UnregisterUser(Intel.RealSense.Face.FaceData faceOutput)
        {
            m_form.Unregister = false;
            if (faceOutput.NumberOfDetectedFaces <= 0)
            {
                return;
            }

            var qface = faceOutput.QueryFaceByIndex(0);
            if (qface == null)
            {
                throw new Exception("PXCMFaceData.Face null");
            }

            Intel.RealSense.Face.RecognitionData rdata = qface.Recognition;
            if (rdata == null)
            {
                throw new Exception(" PXCMFaceData.RecognitionData null");
            }

            if (!rdata.Registered)
            {
                return;
            }
            rdata.UnregisterUser();
        }
    }
}