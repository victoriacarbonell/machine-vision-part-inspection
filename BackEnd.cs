/*!
 * \file    BackEnd.cs
 * \author  IDS Imaging Development Systems GmbH
 * \date    2022-06-01
 * \since   1.1.6
 *
 * \version 1.1.1
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
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using peak.core.nodes;

namespace simple_live_windows_forms
{
    public class BackEnd
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

        private AcquisitionWorker acquisitionWorker;
        private Thread acquisitionThread;

        private peak.core.Device device;
        private peak.core.DataStream dataStream;
        private peak.core.NodeMap nodeMapRemoteDevice;

        private bool isActive;

        public BackEnd()
        {
            Debug.WriteLine("--- [BackEnd] Init");

            isActive = true;

            try
            {
                // Create acquisition worker thread that waits for new images from the camera
                acquisitionWorker = new AcquisitionWorker();
                acquisitionThread = new Thread(new ThreadStart(acquisitionWorker.Start));

                acquisitionWorker.ImageReceived += acquisitionWorker_ImageReceived;
                acquisitionWorker.CounterChanged += acquisitionWorker_CounterChanged;
                acquisitionWorker.MessageBoxTrigger += acquisitionWorker_MessageBoxTrigger;

                // Initialize peak library
                peak.Library.Initialize();
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [BackEnd] Exception: " + e.Message);
            }
        }

        public bool start()
        {
            Debug.WriteLine("--- [BackEnd] Start");
            if (!OpenDevice())
            {
                return false;
            }

            // Start thread execution
            acquisitionThread.Start();

            return true;
        }

        public void Stop()
        {
            Debug.WriteLine("--- [BackEnd] Stop");
            isActive = false;
            acquisitionWorker.Stop();

            if (acquisitionThread.IsAlive)
            {
                acquisitionThread.Join();
            }

            CloseDevice();

            // Close peak library
            peak.Library.Close();
        }

        /*
        public void LoadCameraParameters(string file)
        {
            try
            {
                nodeMapRemoteDevice.LoadFromFile(file);
            }
            catch (System.IO.FileNotFoundException)
            {
                Debug.WriteLine("El archivo de parámetros no se encontró: " + file);
                MessageBoxTrigger?.Invoke(this, "Error", "El archivo de parámetros no se encontró:\n" + file);
            }
            catch (System.UnauthorizedAccessException)
            {
                Debug.WriteLine("Permiso denegado al acceder al archivo de parámetros: " + file);
                MessageBoxTrigger?.Invoke(this, "Error", "Permiso denegado al acceder al archivo:\n" + file);
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine("Error de E/S al cargar los parámetros: " + e.Message);
                MessageBoxTrigger?.Invoke(this, "Error", "Error de E/S al cargar los parámetros:\n" + e.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error al cargar los parámetros de la cámara: " + e.Message);
                MessageBoxTrigger?.Invoke(this, "Error", "Error al cargar los parámetros de la cámara:\n" + e.Message);
            }
        }
        */

        public bool OpenDevice()
        {
            Debug.WriteLine("--- [BackEnd] Open device");
            try
            {
                // Create instance of the device manager
                var deviceManager = peak.DeviceManager.Instance();

                // Update the device manager
                deviceManager.Update();

                // Return if no device was found
                if (!deviceManager.Devices().Any())
                {
                    Debug.WriteLine("--- [BackEnd] Error: No device found");
                    MessageBoxTrigger(this, "Error", "No device found");
                    return false;
                }

                // Open the first openable device in the device manager's device list
                var deviceCount = deviceManager.Devices().Count();

                for (var i = 0; i < deviceCount; ++i)
                {
                    if (deviceManager.Devices()[i].IsOpenable())
                    {
                        device = deviceManager.Devices()[i].OpenDevice(peak.core.DeviceAccessType.Control);

                        // Stop after the first opened device
                        break;
                    }
                    else if (i == (deviceCount - 1))
                    {
                        Debug.WriteLine("--- [BackEnd] Error: Device could not be openend");
                        MessageBoxTrigger(this, "Error", "Device could not be openend");
                        return false;
                    }
                }

                if (device != null)
                {
                    // Check if any datastreams are available
                    var dataStreams = device.DataStreams();

                    if (!dataStreams.Any())
                    {
                        Debug.WriteLine("--- [BackEnd] Error: Device has no DataStream");
                        MessageBoxTrigger(this, "Error", "Device has no DataStream");
                        return false;
                    }

                    try
                    {
                        // Open standard data stream
                        dataStream = dataStreams[0].OpenDataStream();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("--- [BackEnd] Error: Failed to open DataStream");
                        MessageBoxTrigger(this, "Error", "Failed to open DataStream\n" + e.Message);
                        return false;
                    }

                    // Get nodemap of remote device for all accesses to the genicam nodemap tree
                    nodeMapRemoteDevice = device.RemoteDevice().NodeMaps()[0];

                    // Cargar parámetros de la cámara desde archivo
                    // LoadCameraParameters("C:/Users/Alumno/Documents/camara_IDS_Victoria/parametros_camara_victoria.init");


                    // To prepare for untriggered continuous image acquisition, load the default user set if available
                    // and wait until execution is finished
                    try
                    {
                        nodeMapRemoteDevice.FindNode<peak.core.nodes.EnumerationNode>("UserSetSelector").SetCurrentEntry("Default");
                        nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("UserSetLoad").Execute();
                        nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("UserSetLoad").WaitUntilDone();
                    }
                    catch
                    {
                        // UserSet is not available
                    }

                    // Get the payload size for correct buffer allocation
                    UInt32 payloadSize = Convert.ToUInt32(nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("PayloadSize").Value());

                    // Get the minimum number of buffers that must be announced
                    var bufferCountMax = dataStream.NumBuffersAnnouncedMinRequired();

                    // Allocate and announce image buffers and queue them
                    for (var bufferCount = 0; bufferCount < bufferCountMax; ++bufferCount)
                    {
                        var buffer = dataStream.AllocAndAnnounceBuffer(payloadSize, IntPtr.Zero);
                        dataStream.QueueBuffer(buffer);
                    }

                    // Configure worker
                    acquisitionWorker.SetDataStream(dataStream);
                    acquisitionWorker.SetNodemapRemoteDevice(nodeMapRemoteDevice);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [BackEnd] Exception: " + e.Message);
                MessageBoxTrigger(this, "Exception", e.Message);
                return false;
            }

            return true;
        }

        public void CloseDevice()
        {
            Debug.WriteLine("--- [BackEnd] Close device");
            // If device was opened, try to stop acquisition
            if (device != null)
            {
                try
                {
                    var remoteNodeMap = device.RemoteDevice().NodeMaps()[0];
                    remoteNodeMap.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").Execute();
                    remoteNodeMap.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").WaitUntilDone();

                    // dispose no longer used objects
                    remoteNodeMap.Dispose();
                    remoteNodeMap = null;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("--- [BackEnd] Exception: " + e.Message);
                    MessageBoxTrigger(this, "Exception", e.Message);
                }
            }

            // If data stream was opened, try to stop it and revoke its image buffers
            if (dataStream != null)
            {
                try
                {
                    dataStream.KillWait();
                    dataStream.StopAcquisition(peak.core.AcquisitionStopMode.Default);
                    dataStream.Flush(peak.core.DataStreamFlushMode.DiscardAll);

                    foreach (var buffer in dataStream.AnnouncedBuffers())
                    {
                        dataStream.RevokeBuffer(buffer);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("--- [BackEnd] Exception: " + e.Message);
                    MessageBoxTrigger(this, "Exception", e.Message);
                }
            }

            try
            {
                // Unlock parameters after acquisition stop
                nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(0);
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [BackEnd] Exception: " + e.Message);
                MessageBoxTrigger(this, "Exception", e.Message);
            }

            // dispose no longer used objects
            nodeMapRemoteDevice?.Dispose();
            nodeMapRemoteDevice = null;

            dataStream?.Dispose();
            dataStream = null;

            device?.Dispose();
            device = null;
        }

        private void acquisitionWorker_ImageReceived(object sender, System.Drawing.Bitmap image)
        {
            ImageReceived(sender, image);
        }

        private void acquisitionWorker_CounterChanged(object sender, uint frameCounter, uint errorCounter)
        {
            CounterChanged(sender, frameCounter, errorCounter);
        }

        private void acquisitionWorker_MessageBoxTrigger(object sender, String messageTitle, String messageText)
        {
            MessageBoxTrigger(sender, messageTitle, messageText);
        }

        public bool IsActive()
        {
            return isActive;
        }

        // Método para establecer la exposición de la cámara en milisegundos
        public void SetExposure(double valorMs)
        {
            try
            {
                // Buscar el nodo “ExposureTime”
                var nodoExpo = nodeMapRemoteDevice.FindNode<FloatNode>("ExposureTime");
                
                if (nodoExpo != null && nodoExpo.IsWriteable())
                {
                    // Convertimos valorMs (milisegundos) a microsegundos:
                    double valorUs = valorMs * 1000.0;

                    double minUs = Convert.ToDouble(nodoExpo.Minimum());
                    double maxUs = Convert.ToDouble(nodoExpo.Maximum());

                    // Recortamos al rango válido de exposición
                    double nuevoUs = Math.Max(minUs, Math.Min(maxUs, valorUs));

                    // Establecemos el nuevo valor de exposición
                    nodoExpo.SetValue(nuevoUs);
                }
            }
            catch
            {
                Debug.WriteLine("--- [BackEnd] Error al establecer la exposición");
            }
        }

        // Método para establecer la ganancia de la cámara
        public void SetGain(double valor)
        {
            try
            {
                // Buscar el nodo “Gain”
                var nodoGanancia = nodeMapRemoteDevice.FindNode<FloatNode>("Gain");

                if (nodoGanancia != null && nodoGanancia.IsWriteable())
                {
                    double min = nodoGanancia.Minimum();
                    double max = nodoGanancia.Maximum();
                    double nuevo = Math.Max(min, Math.Min(max, valor));

                    // Establecemos el nuevo valor de ganancia
                    nodoGanancia.SetValue(nuevo);
                }
            }
            catch
            {
                Debug.WriteLine("--- [BackEnd] Error al establecer la ganancia");
            }
        }

        /*
        // Método para activar o desactivar el modo blanco y negro
        public void SetBlackAndWhite(bool activarBN)
        {
            try
            {
                var nodoPixelFormat = nodeMapRemoteDevice.FindNode<EnumerationNode>("PixelFormat");

                // Enumerar los formatos de píxel disponibles
                if (nodoPixelFormat != null)
                {
                    Debug.WriteLine("Formatos de píxel disponibles:");

                    foreach (var entry in nodoPixelFormat.Entries().ToList())
                    {
                        try
                        {
                            // Obtener el nombre real de la entrada
                            string entryName = entry.Name();

                            // Verificar si está disponible
                            if (entry.IsAvailable())
                            {
                                Debug.WriteLine($"- {entryName} (Valor: {entry.Value})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error al acceder a la entrada: {ex.Message}");
                        }
                    }
                }

                if (nodoPixelFormat != null && nodoPixelFormat.IsWriteable())
                {
                    if (activarBN)
                    {
                        // Cambiar a formato monocromático
                        var entry = nodoPixelFormat.FindEntry("Mono8");
                        if (entry != null && entry.IsAvailable())
                        {
                            nodoPixelFormat.SetCurrentEntry(entry.Value());
                        }
                    }
                    else
                    {
                        // Volver al formato de color original 
                        var entry = nodoPixelFormat.FindEntry("BGR8");
                        if (entry != null && entry.IsAvailable())
                        {
                            nodoPixelFormat.SetCurrentEntry(entry.Value());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al cambiar a blanco y negro: {ex.Message}");
            }
        }
        */

        // Método para establecer los FPS de la cámara
        public void SetFPS(double fps)
        {
            try
            {
                // Nodo “AcquisitionFrameRate”
                var nodoRate = nodeMapRemoteDevice.FindNode<FloatNode>("AcquisitionFrameRate");

                if (nodoRate != null && nodoRate.IsWriteable())
                {
                    double min = Convert.ToDouble(nodoRate.Minimum());
                    double max = Convert.ToDouble(nodoRate.Maximum());
                    double nuevo = Math.Max(min, Math.Min(max, fps));

                    // Establecemos el nuevo valor de FPS
                    nodoRate.SetValue(nuevo);
                }
            }
            catch
            {
                Debug.WriteLine("--- [BackEnd] Error al establecer los FPS");
            }
        }
    }
}
