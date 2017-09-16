/*******************************************************************************

INTEL CORPORATION PROPRIETARY INFORMATION
This software is supplied under the terms of a license agreement or nondisclosure
agreement with Intel Corporation and may not be copied or disclosed except in
accordance with the terms of that agreement
Copyright(c) 2013 Intel Corporation. All Rights Reserved.

*******************************************************************************/

using FaceID;
using System;
using System.Windows.Forms;

namespace DF_FaceTracking.cs
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string MonitorPath = System.Configuration.ConfigurationManager.AppSettings["Monitor"];
            try
            {
                Intel.RealSense.Session session = Intel.RealSense.Session.CreateInstance();
                if (session != null)
                {
                    Application.Run(new MainForm(session));
                    session.Dispose();
                }
            }
            catch (Exception ex)
            {
                string filename = MonitorPath + string.Format("{0}.txt", 0);
                Log log = new Log(filename);
                log.log(ex.Message);
                Environment.Exit(0);
                throw;
            }
           
        }
    }
}
