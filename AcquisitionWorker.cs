/*!
 * \file    AcquisitionWorker.cs
 * \author  IDS Imaging Development Systems GmbH
 * \date    2022-06-01
 * \since   1.1.6
 *
 * \brief   The AcquisitionWorker class is used in a worker thread to capture
 *          images from the device continuously and do an image conversion into
 *          a desired pixel format.
 *
 * \version 1.3.0
 *
 * Copyright (C) 2020 - 2025, IDS Imaging Development Systems GmbH.
 *
 * The information in this document is subject to change without notice
 * and should not be construed as a commitment by IDS Imaging Development Systems GmbH.
 * IDS Imaging Development Systems GmbH does not assume any responsibility for any errors
 * that may appear in this document.
 *
 * This document, or source code, is provided solely as an example of how to utilize
 * IDS Imaging Development Systems GmbH software libraries in a sample application.
 * IDS Imaging Development Systems GmbH does not assume any responsibility
 * for the use or reliability of any portion of this document.
 *
 * General permission to copy or modify is hereby granted.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace simple_live_windows_forms
{
    public class AcquisitionWorker
    {
        // Event which is raised if a new image was received
        public delegate void ImageReceivedEventHandler(object sender, Bitmap image);
        public event ImageReceivedEventHandler ImageReceived;

        // Event which is raised if the counters has changed
        public delegate void CounterChangedEventHandler(object sender, uint frameCounter, uint errorCounter);
        public event CounterChangedEventHandler CounterChanged;

        // Event which is raised if an Error or Exception has occurred
        public delegate void MessageBoxTriggerEventHandler(object sender, String messageTitle, String messageText);
        public event MessageBoxTriggerEventHandler MessageBoxTrigger;

        private peak.core.DataStream dataStream;
        private peak.core.NodeMap nodeMapRemoteDevice;
        private peak.ipl.ImageConverter imageConverter;

        private readonly peak.ipl.PixelFormat targetPixelFormat = new peak.ipl.PixelFormat(peak.ipl.PixelFormatName.BGRa8);

        private bool running;
        private uint frameCounter;
        private uint errorCounter;

        public AcquisitionWorker()
        {
            Debug.WriteLine("--- [AcquisitionWorker] Init");
            running = false;
            frameCounter = 0;
            errorCounter = 0;
        }

        public void Start()
        {
            Debug.WriteLine("--- [AcquisitionWorker] Start Acquisition");
            try
            {
                // Lock critical features to prevent them from changing during acquisition
                nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(1);

                var inputPixelFormat = (peak.ipl.PixelFormatName)nodeMapRemoteDevice
                    .FindNode<peak.core.nodes.EnumerationNode>("PixelFormat")
                    .CurrentEntry()
                    .Value();
                var width = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Width").Value();
                var height = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Height").Value();

                // Pre-allocate conversion buffers to speed up first image conversion
                // while the acquisition is running
                // NOTE: Re-create the image converter, so old conversion buffers get freed
                imageConverter = new peak.ipl.ImageConverter();
                imageConverter.PreAllocateConversion(inputPixelFormat, targetPixelFormat, (uint)width, (uint)height);

                // Start acquisition
                dataStream.StartAcquisition();
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").Execute();
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").WaitUntilDone();
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [AcquisitionWorker] Exception: " + e.Message);
                MessageBoxTrigger(this, "Exception", e.Message);
            }

            running = true;

            while (running)
            {
                try
                {
                    // Get buffer from device's datastream
                    var buffer = dataStream.WaitForFinishedBuffer(1000);

                    // Create IDS peak IPL image
                    // NOTE: This `peak.ipl.Image` still uses the underlying memory of `buffer`
                    var iplImg = ids_peak_ipl_extension.BufferToImage(buffer);

                    // Debayering and convert IDS peak IPL Image to RGB8 format
                    // NOTE: Use `ImageConverter`, since the `ConvertTo` function re-allocates
                    // the conversion buffers on every call
                    iplImg = imageConverter.Convert(iplImg, targetPixelFormat);

                    var width = Convert.ToInt32(iplImg.Width());
                    var height = Convert.ToInt32(iplImg.Height());
                    var stride = Convert.ToInt32(iplImg.PixelFormat().CalculateStorageSizeOfPixels(iplImg.Width()));

                    // Queue buffer so that it can be used again 
                    dataStream.QueueBuffer(buffer);

                    var data = iplImg.Data();

                    var image = new Bitmap(width, height, stride,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb, data);

                    // Create a deep copy of the Bitmap, so it doesn't use memory of the IDS peak IPL Image.
                    // Warning: Don't use image.Clone(), because it only creates a shallow copy!
                    var imageCopy = new Bitmap(image);

                    // The other images are not needed anymore.
                    image.Dispose();
                    iplImg.Dispose();

                    if (ImageReceived != null)
                    {
                        Debug.WriteLine("--- [AcquisitionWorker] Send image Nr. " + (frameCounter + 1));
                        ImageReceived(this, imageCopy);
                    }

                    frameCounter++;
                }
                catch (Exception e)
                {
                    errorCounter++;
                    Debug.WriteLine("--- [AcquisitionWorker] Exception: " + e.Message);
                    MessageBoxTrigger(this, "Exception", e.Message);
                }

                // Raise event with current frame and error counter
                CounterChanged(this, frameCounter, errorCounter);
            }
        }

        public void Stop()
        {
            Debug.WriteLine("--- [AcquisitionWorker] Stop Acquisition");
            running = false;
        }

        public void SetDataStream(peak.core.DataStream dataStream)
        {
            Debug.WriteLine("--- [AcquisitionWorker] Set dataStream");
            this.dataStream = dataStream;
        }

        public void SetNodemapRemoteDevice(peak.core.NodeMap nodeMap)
        {
            Debug.WriteLine("--- [AcquisitionWorker] Set nodeMap");
            nodeMapRemoteDevice = nodeMap;
        }
    }
}
