/*!
 * \file    FormWindow.cs
 * \author  IDS Imaging Development Systems GmbH
 * \date    2022-06-01
 * \since   1.1.6
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

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
// using Emgu.CV.Bitmap;
// using Emgu.Platform.NetSatndard;

namespace simple_live_windows_forms
{
    public partial class FormWindow : Form
    {
        private String projectName;
        private String version;
        private SimplePictureBox pictureBox;
        private BackEnd backEnd;
        private BackgroundWorker backgroundWorker1;
        private Panel labelPanel;
        private Label counterLabel;
        private Label infoLabel;
        private bool hasError;

        public FormWindow()
        {
            projectName = "simple_live_windows_forms";
            version = "v1.3.1";

            Debug.WriteLine("--- [FormWindow] Init");
            try
            {
                InitializeComponent();
                this.infoLabel.Text = projectName + " " + version;
                this.Text = projectName;

                backEnd = new BackEnd();

                FormClosing += FormWindow_FormClosing;

                backEnd.ImageReceived += backEnd_ImageReceived;
                backEnd.CounterChanged += backEnd_CounterChanged;
                backEnd.MessageBoxTrigger += backEnd_MessageBoxTrigger;

                if (backEnd.start())
                {
                    hasError = false;
                }
                else
                {
                    hasError = true;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [FormWindow] Exception: " + e.Message);
                backEnd_MessageBoxTrigger(this, "Exception", e.Message);
            }
        }

        // Contenedores para los frames de vídeo y sus conversiones
        Image<Bgr, byte> frameBGR;
        Image<Gray, byte> frameGray;
        Image<Gray, byte> frameBin;

        private void backEnd_ImageReceived(object sender, Bitmap image)
        {
            // Mostrar la imagen en bruto en el PictureBox
            if (pictureBox.InvokeRequired)
            {
                pictureBox.Invoke(new Action(() =>
                {
                    pictureBox.Image?.Dispose();
                    pictureBox.Image = (Bitmap)image.Clone();
                }));
            }
            else
            {
                pictureBox.Image?.Dispose();
                pictureBox.Image = (Bitmap)image.Clone();
            }

            /*
            try
            {
                pictureBox.Image = image;
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [FormWindow] Exception: " + e.Message);
                backEnd_MessageBoxTrigger(this, "Exception", e.Message);
            }
            */

            // Capturamos el frame actual del vídeo y lo convertimos a Image<Bgr, byte>
            try
            {
                // Frame en formato BGR
                frameBGR = image.ToImage<Bgr, byte>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al convertir el frame a Image<Bgr, byte>: {ex.Message}");
                return;
            }

            // Binarizamos el frame actual
            try
            {
                // Frame en escala de grises
                frameGray = frameBGR.Convert<Gray, byte>();

                // Frame binarizado
                frameBin = frameGray.ThresholdBinaryInv(new Gray(100), new Gray(255));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al convertir el frame a binario: {ex.Message}");

                // Liberamos lo que ya existe antes de salir
                frameBGR.Dispose();
                return;
            }

            // Identificar piezas en el frame actual
            if (frameBin != null)
            {
                // Identificar contornos y jerarquía en el frame binario
                try
                {
                    // Encontrar las piezas (contornos) y la jerarquía en el frame binario
                    VectorOfVectorOfPoint contornos = new VectorOfVectorOfPoint();
                    Mat jerarquia = new Mat(); // Matriz para almacenar la jerarquía de los contornos
                    CvInvoke.FindContours(frameBin, contornos, jerarquia, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

                    // Convertir la jerarquia a una matriz de enteros
                    int[] jerarquiaDatos = jerarquia.GetData(false).Cast<int>().ToArray();

                    // Inicializar el nº de agujeros
                    int numAgujeros = 0;

                    // Analizar cada contorno
                    for (int i = 0; i < contornos.Size; i++)
                    {
                        // Comprobar si el contorno tiene agujeros
                        if (jerarquiaDatos != null)
                        {
                            int padre = jerarquiaDatos[i * 4 + 3]; // Índice del contorno padre

                            // Si este contorno es padre
                            if (padre == -1)
                            {
                                // Contar los contornos de dentro
                                numAgujeros = jerarquiaDatos.Where((p, j) => ((j + 1) % 4 == 0) && p == i).Count();
                            }
                            else
                            {
                                continue; // Si es un contorno hijo, lo ignoramos
                            }
                        }

                        // Obtener el contorno externo i
                        var contorno = contornos[i];

                        // Propiedades del contorno
                        double area = CvInvoke.ContourArea(contorno); // Área del contorno
                        double perimetro = CvInvoke.ArcLength(contorno, true); // Perímetro del contorno
                        Rectangle rectanguloDelimitador = CvInvoke.BoundingRectangle(contorno); // Rectángulo que encierra completamente el contorno

                        double relacionAspecto = (double)rectanguloDelimitador.Width / rectanguloDelimitador.Height; // Relación entre el ancho y el alto del rectángulo delimitador
                        double circularidad = 4 * Math.PI * area / (perimetro * perimetro); // Circularidad = 1 para un círculo perfecto
                        double solidez = area / (rectanguloDelimitador.Width * rectanguloDelimitador.Height); // Solidez = relación entre el área del contorno y el área del rectángulo delimitador

                        // Aproximar el contorno a un polígono
                        double epsilon = 0.02 * perimetro; // Epsilon es un parámetro que controla la precisión de la aproximación
                        var aproximacionPoligono = new VectorOfPoint();
                        CvInvoke.ApproxPolyDP(contorno, aproximacionPoligono, epsilon, true);

                        // Contar el número de vértices del polígono aproximado
                        int numVertices = aproximacionPoligono.Size;

                        // Determinar si el polígono es convexo o cóncavo
                        bool esConvexo = CvInvoke.IsContourConvex(aproximacionPoligono); // true si es convexo (no tiene forma de L)

                        // Determinar el tipo de pieza según las propiedades
                        string tipo = "Pieza desconocida";
                        // string datos = $"Contorno {i}: Área = {area}, Perímetro = {perimetro}, Relación de aspecto = {relacionAspecto}, Circularidad = {circularidad}, Agujeros = {numAgujeros}, Vértices = {numVertices}, Convexa = {esConvexo}, Solidez = {solidez}";
                        // MessageBox.Show(datos, "Datos del contorno");

                        if (circularidad > 0.75 && (relacionAspecto > 0.75 && relacionAspecto < 1.25) && numAgujeros == 1 && numVertices > 7) // Si la circularidad es casi 1, la relación de aspecto es cercana a 1 (cuadrado) y tiene agujero, será una arandela
                        {
                            tipo = "Arandela";
                        }
                        else if (numAgujeros == 1 && (numVertices >= 5 && numVertices <= 7)) // Si tiene 1 agujero y el polígono es un hexágono o heptágono, será una tuerca
                        {
                            tipo = "Tuerca";
                        }
                        else if (numAgujeros > 1 && !esConvexo && area > 14000) // Si tiene varios agujeros, no es convexa y el área es grande, será una placa de unión de soportes en forma de L
                        {
                            tipo = "Placa union soportes en forma de L";
                        }
                        else if (numAgujeros > 1 && esConvexo && area > 14000) // Si tiene varios agujeros, es convexa y el área es grande, será una placa de unión de soportes recta
                        {
                            tipo = "Placa union soportes recta";
                        }
                        else if (area < 3000 && circularidad < 0.5) // Si el área es pequeña y no es circular, será un tornillo
                        {
                            tipo = "Tornillo";
                        }
                        else if (numAgujeros == 0 && !esConvexo && area > 5000 && solidez < 0.25) // Si no tiene agujeros, no es convexa, el área es grande y la solidez es muy baja, será una llave Allen
                        {
                            tipo = "Llave Allen";
                        }
                        else if (!esConvexo && (area > 3000 && area < 5000) && numVertices > 7 && numAgujeros == 0) // Si no es convexa, el área es pequeña, tiene muchos vértices y no tiene agujeros, será un gancho
                        {
                            tipo = "Gancho";
                        }

                        // Dibujar el contorno de la pieza en el frame
                        CvInvoke.DrawContours(frameBGR, contornos, i, new MCvScalar(0, 255, 0), 2); // Dibujar el contorno en verde

                        // Configuración del texto
                        Emgu.CV.CvEnum.FontFace tipoFuente = Emgu.CV.CvEnum.FontFace.HersheySimplex; // Tipo de fuente
                        double escalaFuente = 1.5; // Escala del texto
                        int grosorFuente = 3; // Grosor del texto
                        int baseline = 0; // Variable para almacenar la línea base del texto

                        // Obtener el tamaño del texto
                        Size textSize = CvInvoke.GetTextSize(
                             tipo,
                             tipoFuente,
                             escalaFuente,
                             grosorFuente,
                             ref baseline);

                        // Posición del texto
                        int xTexto = rectanguloDelimitador.X; // Coordenada X
                        int yTexto = rectanguloDelimitador.Y + textSize.Height - 30; // Coordenada Y

                        // Rectángulo de fondo (negro) para el texto
                        var rectanguloFondo = new Rectangle(
                            xTexto, // Coordenada X del rectángulo
                            yTexto - textSize.Height,   // Coordenada Y del rectángulo
                            textSize.Width, // Anchura rectángulo
                            textSize.Height + baseline); // Altura rectángulo

                        // Dibujar un rectángulo de fondo para el texto
                        CvInvoke.Rectangle(
                            frameBGR, // Imagen sobre la que se dibuja el rectángulo
                            rectanguloFondo, // Rectángulo a dibujar
                            new MCvScalar(0, 0, 0),  // Color negro
                            -1);                     // grosor = -1 → relleno completo

                        // Dibujar el nombre de cada pieza sobre el rectángulo
                        Point textPoint = new Point(xTexto, yTexto);
                        CvInvoke.PutText(
                            frameBGR, // Imagen sobre la que se dibuja el texto
                            tipo, // Texto a dibujar
                            textPoint, // Punto donde se dibuja el texto
                            Emgu.CV.CvEnum.FontFace.HersheySimplex, // Tipo de fuente
                            escalaFuente, // Escala del texto
                            new MCvScalar(255, 255, 255),  // Color blanco
                            grosorFuente); // Grosor del texto
                    }

                    // Convertir el frame BGR (con el texto escrito) a Bitmap
                    Bitmap frameBGRBitmap = frameBGR.ToBitmap();

                    // Asignar el Bitmap al PictureBox de identificar piezas en tiempo real
                    if (pbtVideoTiempoReal.InvokeRequired)
                    {
                        pbtVideoTiempoReal.Invoke(new Action(() =>
                        {
                            pbtVideoTiempoReal.Image?.Dispose();
                            pbtVideoTiempoReal.Image = frameBGRBitmap;
                        }));
                    }
                    else
                    {
                        pbtVideoTiempoReal.Image?.Dispose();
                        pbtVideoTiempoReal.Image = frameBGRBitmap;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error al identificar piezas: " + ex.Message);
                    frameBGR.Dispose();
                    frameGray.Dispose();
                    frameBin.Dispose();
                    return;
                }
            }
            else
            {
                Debug.WriteLine("No hay imagen cargada para identificar piezas.");
            }
        }

        private void backEnd_CounterChanged(object sender, uint frameCounter, uint errorCounter)
        {
            if (counterLabel.InvokeRequired)
            {
                counterLabel.BeginInvoke((MethodInvoker)delegate { counterLabel.Text = "Frames acquired: " + frameCounter + ", errors: " + errorCounter; });
            }
        }

        private void backEnd_MessageBoxTrigger(object sender, String messageTitle, String messageText)
        {
            MessageBox.Show(messageText, messageTitle);
        }

        private void FormWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Si backEnd sigue activo, lo detenemos en un task y cancelamos el cierre inmediato
            if (backEnd != null && backEnd.IsActive())
            {
                // Evitamos que WinForms cierre ahora mismo:
                e.Cancel = true;

                Task.Run(() =>
                {
                    // Esto bloquea hasta que se interrumpa la adquisición de la cámara
                    backEnd.Stop();

                    // Una vez detenido el backEnd, volvemos a la UI para cerrar la aplicación
                    this.Invoke(new Action(() =>
                    {
                        Application.Exit();
                    }));
                });
            }
            else
            {
                // Si backEnd ya estaba detenido, cerramos inmediatamente
                Application.Exit();
            }

            Debug.WriteLine("--- [FormWindow] Closing (iniciado)");
        }

        public bool HasError()
        {
            return hasError;
        }

        private void InitializeComponent()
        {
            pictureBox = new SimplePictureBox();
            backgroundWorker1 = new BackgroundWorker();
            labelPanel = new Panel();
            infoLabel = new Label();
            counterLabel = new Label();
            btnCapturarImagen = new Button();
            btnIdentificarPiezas = new Button();
            btnConfiguracion = new Button();
            btnSalir = new Button();
            pbtImage = new SimplePictureBox();
            btnCargarImagen = new Button();
            btnIdentificarPiezasVideo = new Button();
            label1 = new Label();
            label2 = new Label();
            groupBox1 = new GroupBox();
            lblNumLlavesAllenVideo = new Label();
            lblNumGanchosVideo = new Label();
            lblNumTuercasVideo = new Label();
            lblNumPlacasUnionSoportesLVideo = new Label();
            lblNumPlacasUnionSoportesRectasVideo = new Label();
            lblNumArandelasVideo = new Label();
            lblNumTornillosVideo = new Label();
            lblIdentificarPiezasVideo = new Label();
            groupBox2 = new GroupBox();
            lblNumLlavesAllenImagen = new Label();
            lblIdentificarPiezasImagen = new Label();
            lblNumGanchosImagen = new Label();
            lblNumPlacasUnionSoportesRectasImagen = new Label();
            lblNumTuercasImagen = new Label();
            lblNumTornillosImagen = new Label();
            lblNumPlacasUnionSoportesLImagen = new Label();
            lblNumArandelasImagen = new Label();
            label3 = new Label();
            pbtVideoTiempoReal = new SimplePictureBox();
            ((ISupportInitialize)pictureBox).BeginInit();
            labelPanel.SuspendLayout();
            ((ISupportInitialize)pbtImage).BeginInit();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((ISupportInitialize)pbtVideoTiempoReal).BeginInit();
            SuspendLayout();
            // 
            // pictureBox
            // 
            pictureBox.Image = null;
            pictureBox.Location = new Point(12, 59);
            pictureBox.Name = "pictureBox";
            pictureBox.Size = new Size(503, 310);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 1;
            pictureBox.TabStop = false;
            pictureBox.Click += pictureBox_Click;
            // 
            // labelPanel
            // 
            labelPanel.Controls.Add(infoLabel);
            labelPanel.Controls.Add(counterLabel);
            labelPanel.Location = new Point(12, 609);
            labelPanel.Name = "labelPanel";
            labelPanel.Size = new Size(1556, 15);
            labelPanel.TabIndex = 2;
            // 
            // infoLabel
            // 
            infoLabel.AutoSize = true;
            infoLabel.Dock = DockStyle.Right;
            infoLabel.Location = new Point(1402, 0);
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new Size(154, 15);
            infoLabel.TabIndex = 2;
            infoLabel.Text = "Lenguajes de Programación";
            infoLabel.Click += infoLabel_Click;
            // 
            // counterLabel
            // 
            counterLabel.AutoSize = true;
            counterLabel.Dock = DockStyle.Left;
            counterLabel.Location = new Point(0, 0);
            counterLabel.Name = "counterLabel";
            counterLabel.Size = new Size(147, 15);
            counterLabel.TabIndex = 1;
            counterLabel.Text = "Victoria Carbonell Sánchez";
            // 
            // btnCapturarImagen
            // 
            btnCapturarImagen.Location = new Point(209, 30);
            btnCapturarImagen.Name = "btnCapturarImagen";
            btnCapturarImagen.Size = new Size(117, 23);
            btnCapturarImagen.TabIndex = 3;
            btnCapturarImagen.Text = "Capturar imagen";
            btnCapturarImagen.UseVisualStyleBackColor = true;
            btnCapturarImagen.Click += btnCapturarImagen_Click;
            // 
            // btnIdentificarPiezas
            // 
            btnIdentificarPiezas.Location = new Point(1058, 375);
            btnIdentificarPiezas.Name = "btnIdentificarPiezas";
            btnIdentificarPiezas.Size = new Size(183, 23);
            btnIdentificarPiezas.TabIndex = 4;
            btnIdentificarPiezas.Text = "Identificar pieza(s) en la imagen";
            btnIdentificarPiezas.UseVisualStyleBackColor = true;
            btnIdentificarPiezas.Click += btnIdentificarPiezas_Click;
            // 
            // btnConfiguracion
            // 
            btnConfiguracion.Location = new Point(332, 30);
            btnConfiguracion.Name = "btnConfiguracion";
            btnConfiguracion.Size = new Size(183, 23);
            btnConfiguracion.TabIndex = 5;
            btnConfiguracion.Text = "Configuración de la cámara";
            btnConfiguracion.UseVisualStyleBackColor = true;
            btnConfiguracion.Click += btnConfiguracion_Click;
            // 
            // btnSalir
            // 
            btnSalir.Location = new Point(1444, 569);
            btnSalir.Name = "btnSalir";
            btnSalir.Size = new Size(117, 23);
            btnSalir.TabIndex = 6;
            btnSalir.Text = "Salir";
            btnSalir.UseVisualStyleBackColor = true;
            btnSalir.Click += btnSalir_Click;
            // 
            // pbtImage
            // 
            pbtImage.Image = null;
            pbtImage.Location = new Point(1058, 59);
            pbtImage.Name = "pbtImage";
            pbtImage.Size = new Size(503, 310);
            pbtImage.SizeMode = PictureBoxSizeMode.Zoom;
            pbtImage.TabIndex = 7;
            pbtImage.TabStop = false;
            // 
            // btnCargarImagen
            // 
            btnCargarImagen.Location = new Point(1334, 30);
            btnCargarImagen.Name = "btnCargarImagen";
            btnCargarImagen.Size = new Size(227, 23);
            btnCargarImagen.TabIndex = 8;
            btnCargarImagen.Text = "Cargar imagen desde archivo";
            btnCargarImagen.UseVisualStyleBackColor = true;
            btnCargarImagen.Click += btnCargarImagen_Click;
            // 
            // btnIdentificarPiezasVideo
            // 
            btnIdentificarPiezasVideo.Location = new Point(12, 375);
            btnIdentificarPiezasVideo.Name = "btnIdentificarPiezasVideo";
            btnIdentificarPiezasVideo.Size = new Size(183, 23);
            btnIdentificarPiezasVideo.TabIndex = 9;
            btnIdentificarPiezasVideo.Text = "Identificar pieza(s) en el vídeo";
            btnIdentificarPiezasVideo.UseVisualStyleBackColor = true;
            btnIdentificarPiezasVideo.Click += btnIdentificarPiezasVideo_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label1.Location = new Point(12, 32);
            label1.Name = "label1";
            label1.Size = new Size(191, 21);
            label1.TabIndex = 10;
            label1.Text = "VÍDEO EN TIEMPO REAL";
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label2.Location = new Point(1058, 32);
            label2.Name = "label2";
            label2.Size = new Size(270, 21);
            label2.TabIndex = 11;
            label2.Text = "IMAGEN CAPTURADA O CARGADA";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(lblNumLlavesAllenVideo);
            groupBox1.Controls.Add(lblNumGanchosVideo);
            groupBox1.Controls.Add(lblNumTuercasVideo);
            groupBox1.Controls.Add(lblNumPlacasUnionSoportesLVideo);
            groupBox1.Controls.Add(lblNumPlacasUnionSoportesRectasVideo);
            groupBox1.Controls.Add(lblNumArandelasVideo);
            groupBox1.Controls.Add(lblNumTornillosVideo);
            groupBox1.Controls.Add(lblIdentificarPiezasVideo);
            groupBox1.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            groupBox1.Location = new Point(12, 404);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(503, 149);
            groupBox1.TabIndex = 13;
            groupBox1.TabStop = false;
            groupBox1.Text = "PIEZAS IDENTIFICADAS EN EL VÍDEO";
            groupBox1.Enter += groupBox1_Enter;
            // 
            // lblNumLlavesAllenVideo
            // 
            lblNumLlavesAllenVideo.AutoSize = true;
            lblNumLlavesAllenVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumLlavesAllenVideo.Location = new Point(6, 120);
            lblNumLlavesAllenVideo.Name = "lblNumLlavesAllenVideo";
            lblNumLlavesAllenVideo.Size = new Size(116, 19);
            lblNumLlavesAllenVideo.TabIndex = 7;
            lblNumLlavesAllenVideo.Text = "- Llaves Allen = 0";
            lblNumLlavesAllenVideo.Click += lblNumLlavesAllenVideo_Click;
            // 
            // lblNumGanchosVideo
            // 
            lblNumGanchosVideo.AutoSize = true;
            lblNumGanchosVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumGanchosVideo.Location = new Point(6, 82);
            lblNumGanchosVideo.Name = "lblNumGanchosVideo";
            lblNumGanchosVideo.Size = new Size(98, 19);
            lblNumGanchosVideo.TabIndex = 6;
            lblNumGanchosVideo.Text = "- Ganchos = 0";
            // 
            // lblNumTuercasVideo
            // 
            lblNumTuercasVideo.AutoSize = true;
            lblNumTuercasVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumTuercasVideo.Location = new Point(6, 101);
            lblNumTuercasVideo.Name = "lblNumTuercasVideo";
            lblNumTuercasVideo.Size = new Size(91, 19);
            lblNumTuercasVideo.TabIndex = 5;
            lblNumTuercasVideo.Text = "- Tuercas = 0";
            lblNumTuercasVideo.Click += label5_Click;
            // 
            // lblNumPlacasUnionSoportesLVideo
            // 
            lblNumPlacasUnionSoportesLVideo.AutoSize = true;
            lblNumPlacasUnionSoportesLVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumPlacasUnionSoportesLVideo.Location = new Point(169, 63);
            lblNumPlacasUnionSoportesLVideo.Name = "lblNumPlacasUnionSoportesLVideo";
            lblNumPlacasUnionSoportesLVideo.Size = new Size(305, 19);
            lblNumPlacasUnionSoportesLVideo.TabIndex = 4;
            lblNumPlacasUnionSoportesLVideo.Text = "- Placas de unión de soportes en forma de L = 0";
            lblNumPlacasUnionSoportesLVideo.Click += label4_Click;
            // 
            // lblNumPlacasUnionSoportesRectasVideo
            // 
            lblNumPlacasUnionSoportesRectasVideo.AutoSize = true;
            lblNumPlacasUnionSoportesRectasVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumPlacasUnionSoportesRectasVideo.Location = new Point(169, 44);
            lblNumPlacasUnionSoportesRectasVideo.Name = "lblNumPlacasUnionSoportesRectasVideo";
            lblNumPlacasUnionSoportesRectasVideo.Size = new Size(256, 19);
            lblNumPlacasUnionSoportesRectasVideo.TabIndex = 3;
            lblNumPlacasUnionSoportesRectasVideo.Text = "- Placas de unión de soportes rectas = 0";
            // 
            // lblNumArandelasVideo
            // 
            lblNumArandelasVideo.AutoSize = true;
            lblNumArandelasVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumArandelasVideo.Location = new Point(6, 63);
            lblNumArandelasVideo.Name = "lblNumArandelasVideo";
            lblNumArandelasVideo.Size = new Size(105, 19);
            lblNumArandelasVideo.TabIndex = 2;
            lblNumArandelasVideo.Text = "- Arandelas = 0";
            // 
            // lblNumTornillosVideo
            // 
            lblNumTornillosVideo.AutoSize = true;
            lblNumTornillosVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumTornillosVideo.Location = new Point(6, 44);
            lblNumTornillosVideo.Name = "lblNumTornillosVideo";
            lblNumTornillosVideo.Size = new Size(95, 19);
            lblNumTornillosVideo.TabIndex = 1;
            lblNumTornillosVideo.Text = "- Tornillos = 0";
            // 
            // lblIdentificarPiezasVideo
            // 
            lblIdentificarPiezasVideo.AutoSize = true;
            lblIdentificarPiezasVideo.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblIdentificarPiezasVideo.Location = new Point(6, 25);
            lblIdentificarPiezasVideo.Name = "lblIdentificarPiezasVideo";
            lblIdentificarPiezasVideo.Size = new Size(324, 19);
            lblIdentificarPiezasVideo.TabIndex = 0;
            lblIdentificarPiezasVideo.Text = "Pulse el botón para identificar las piezas en el vídeo.";
            lblIdentificarPiezasVideo.Click += lblIdentificarPiezasVideo_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(lblNumLlavesAllenImagen);
            groupBox2.Controls.Add(lblIdentificarPiezasImagen);
            groupBox2.Controls.Add(lblNumGanchosImagen);
            groupBox2.Controls.Add(lblNumPlacasUnionSoportesRectasImagen);
            groupBox2.Controls.Add(lblNumTuercasImagen);
            groupBox2.Controls.Add(lblNumTornillosImagen);
            groupBox2.Controls.Add(lblNumPlacasUnionSoportesLImagen);
            groupBox2.Controls.Add(lblNumArandelasImagen);
            groupBox2.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            groupBox2.Location = new Point(1058, 404);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(503, 149);
            groupBox2.TabIndex = 14;
            groupBox2.TabStop = false;
            groupBox2.Text = "PIEZAS IDENTIFICADAS EN LA IMAGEN";
            // 
            // lblNumLlavesAllenImagen
            // 
            lblNumLlavesAllenImagen.AutoSize = true;
            lblNumLlavesAllenImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumLlavesAllenImagen.Location = new Point(9, 120);
            lblNumLlavesAllenImagen.Name = "lblNumLlavesAllenImagen";
            lblNumLlavesAllenImagen.Size = new Size(116, 19);
            lblNumLlavesAllenImagen.TabIndex = 14;
            lblNumLlavesAllenImagen.Text = "- Llaves Allen = 0";
            // 
            // lblIdentificarPiezasImagen
            // 
            lblIdentificarPiezasImagen.AutoSize = true;
            lblIdentificarPiezasImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblIdentificarPiezasImagen.Location = new Point(6, 25);
            lblIdentificarPiezasImagen.Name = "lblIdentificarPiezasImagen";
            lblIdentificarPiezasImagen.Size = new Size(336, 19);
            lblIdentificarPiezasImagen.TabIndex = 1;
            lblIdentificarPiezasImagen.Text = "Pulse el botón para identificar las piezas en la imagen.";
            // 
            // lblNumGanchosImagen
            // 
            lblNumGanchosImagen.AutoSize = true;
            lblNumGanchosImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumGanchosImagen.Location = new Point(9, 82);
            lblNumGanchosImagen.Name = "lblNumGanchosImagen";
            lblNumGanchosImagen.Size = new Size(98, 19);
            lblNumGanchosImagen.TabIndex = 13;
            lblNumGanchosImagen.Text = "- Ganchos = 0";
            // 
            // lblNumPlacasUnionSoportesRectasImagen
            // 
            lblNumPlacasUnionSoportesRectasImagen.AutoSize = true;
            lblNumPlacasUnionSoportesRectasImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumPlacasUnionSoportesRectasImagen.Location = new Point(172, 44);
            lblNumPlacasUnionSoportesRectasImagen.Name = "lblNumPlacasUnionSoportesRectasImagen";
            lblNumPlacasUnionSoportesRectasImagen.Size = new Size(256, 19);
            lblNumPlacasUnionSoportesRectasImagen.TabIndex = 10;
            lblNumPlacasUnionSoportesRectasImagen.Text = "- Placas de unión de soportes rectas = 0";
            // 
            // lblNumTuercasImagen
            // 
            lblNumTuercasImagen.AutoSize = true;
            lblNumTuercasImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumTuercasImagen.Location = new Point(9, 101);
            lblNumTuercasImagen.Name = "lblNumTuercasImagen";
            lblNumTuercasImagen.Size = new Size(91, 19);
            lblNumTuercasImagen.TabIndex = 12;
            lblNumTuercasImagen.Text = "- Tuercas = 0";
            // 
            // lblNumTornillosImagen
            // 
            lblNumTornillosImagen.AutoSize = true;
            lblNumTornillosImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumTornillosImagen.Location = new Point(9, 44);
            lblNumTornillosImagen.Name = "lblNumTornillosImagen";
            lblNumTornillosImagen.Size = new Size(95, 19);
            lblNumTornillosImagen.TabIndex = 8;
            lblNumTornillosImagen.Text = "- Tornillos = 0";
            // 
            // lblNumPlacasUnionSoportesLImagen
            // 
            lblNumPlacasUnionSoportesLImagen.AutoSize = true;
            lblNumPlacasUnionSoportesLImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumPlacasUnionSoportesLImagen.Location = new Point(172, 63);
            lblNumPlacasUnionSoportesLImagen.Name = "lblNumPlacasUnionSoportesLImagen";
            lblNumPlacasUnionSoportesLImagen.Size = new Size(305, 19);
            lblNumPlacasUnionSoportesLImagen.TabIndex = 11;
            lblNumPlacasUnionSoportesLImagen.Text = "- Placas de unión de soportes en forma de L = 0";
            // 
            // lblNumArandelasImagen
            // 
            lblNumArandelasImagen.AutoSize = true;
            lblNumArandelasImagen.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblNumArandelasImagen.Location = new Point(9, 63);
            lblNumArandelasImagen.Name = "lblNumArandelasImagen";
            lblNumArandelasImagen.Size = new Size(105, 19);
            lblNumArandelasImagen.TabIndex = 9;
            lblNumArandelasImagen.Text = "- Arandelas = 0";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label3.Location = new Point(535, 32);
            label3.Name = "label3";
            label3.Size = new Size(352, 21);
            label3.TabIndex = 19;
            label3.Text = "IDENTIFICACIÓN DE PIEZAS EN TIEMPO REAL";
            label3.Click += label3_Click;
            // 
            // pbtVideoTiempoReal
            // 
            pbtVideoTiempoReal.Image = null;
            pbtVideoTiempoReal.Location = new Point(535, 59);
            pbtVideoTiempoReal.Name = "pbtVideoTiempoReal";
            pbtVideoTiempoReal.Size = new Size(503, 310);
            pbtVideoTiempoReal.SizeMode = PictureBoxSizeMode.Zoom;
            pbtVideoTiempoReal.TabIndex = 15;
            pbtVideoTiempoReal.TabStop = false;
            pbtVideoTiempoReal.Click += pbtVideoTiempoReal_Click;
            // 
            // FormWindow
            // 
            ClientSize = new Size(1580, 624);
            Controls.Add(label3);
            Controls.Add(pbtVideoTiempoReal);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(btnIdentificarPiezasVideo);
            Controls.Add(btnCargarImagen);
            Controls.Add(pbtImage);
            Controls.Add(btnSalir);
            Controls.Add(btnConfiguracion);
            Controls.Add(btnIdentificarPiezas);
            Controls.Add(btnCapturarImagen);
            Controls.Add(labelPanel);
            Controls.Add(pictureBox);
            Name = "FormWindow";
            Text = "Inspección de piezas con visión artificial";
            Load += FormWindow_Load;
            ((ISupportInitialize)pictureBox).EndInit();
            labelPanel.ResumeLayout(false);
            labelPanel.PerformLayout();
            ((ISupportInitialize)pbtImage).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((ISupportInitialize)pbtVideoTiempoReal).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        private void pictureBox_Click(object sender, EventArgs e)
        {

        }

        private Button btnCapturarImagen;
        private Button btnIdentificarPiezas;
        private Button btnConfiguracion;
        private Button btnSalir;

        Image<Bgr, byte> ImageBGR;
        Image<Gray, byte> ImageGray;
        Image<Gray, byte> ImageBin;

        private void btnCapturarImagen_Click(object sender, EventArgs e)
        {
            // Capturar imagen desde la cámara y asignarla al PictureBox
            try
            {
                if (pictureBox.Image != null)
                {
                    // Convertir la imagen del PictureBox a Bitmap
                    Bitmap bmp = new Bitmap(pictureBox.Image);

                    // Convertir Bitmap a Image<Bgr, byte>
                    ImageBGR = bmp.ToImage<Bgr, byte>();

                    // Crear contenedor para la imagen en escala de grises
                    ImageGray = ImageBGR.Convert<Gray, byte>();

                    // Crear contenedor para la imagen binaria
                    ImageBin = ImageGray.ThresholdBinaryInv(new Gray(100), new Gray(255));

                    // Mostrar la imagen binaria en el segundo PictureBox
                    pbtImage.Image = ImageBin.ToBitmap();
                }
                else
                {
                    MessageBox.Show("No hay imagen en el PictureBox.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al capturar la imagen desde la cámara: " + ex.Message);
            }
        }

        private void btnIdentificarPiezas_Click(object sender, EventArgs e)
        {
            // Inicializar contadores para las piezas
            int tornillos = 0; // Contador de tornillos identificados
            int arandelas = 0; // Contador de arandelas identificadas
            int tuercas = 0; // Contador de tuercas identificadas
            int placasUnionSoportesL = 0; // Contador de placas de unión de soportes en forma de L identificadas
            int placasUnionSoportesRecta = 0; // Contador de placas de unión de soportes recta identificadas
            int llavesAllen = 0; // Contador de llaves Allen identificadas
            int ganchos = 0; // Contador de ganchos identificados

            // Inicializar el texto de los contadores
            lblNumArandelasImagen.Text = $"- Arandelas = {arandelas}"; // Actualizar el texto del contador de arandelas
            lblNumTornillosImagen.Text = $"- Tornillos = {tornillos}"; // Actualizar el texto del contador de tornillos
            lblNumTuercasImagen.Text = $"- Tuercas = {tuercas}"; // Actualizar el texto del contador de tuercas
            lblNumPlacasUnionSoportesLImagen.Text = $"- Placas de unión de soportes en forma de L = {placasUnionSoportesL}"; // Actualizar el texto del contador de placas de unión de soportes en forma de L
            lblNumPlacasUnionSoportesRectasImagen.Text = $"- Placas de unión de soportes rectas = {placasUnionSoportesRecta}"; // Actualizar el texto del contador de placas de unión de soportes rectas
            lblNumLlavesAllenImagen.Text = $"- Llaves Allen = {llavesAllen}"; // Actualizar el texto del contador de llaves Allen
            lblNumGanchosImagen.Text = $"- Ganchos = {ganchos}"; // Actualizar el texto del contador de ganchos

            if (pbtImage.Image == null)
            {
                MessageBox.Show("Error: es necesario capturar la imagen que se desea analizar");
                return;
            }

            // Identificar piezas en la imagen capturada
            if (pbtImage.Image != null)
            {
                try
                {
                    // Encontrar las piezas (contornos) y la jerarquía en la imagen binaria
                    VectorOfVectorOfPoint contornos = new VectorOfVectorOfPoint();
                    Mat jerarquia = new Mat(); // Matriz para almacenar la jerarquía de los contornos
                    CvInvoke.FindContours(ImageBin, contornos, jerarquia, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

                    // Convertir la jerarquia a una matriz de enteros
                    int[] jerarquiaDatos = jerarquia.GetData(false).Cast<int>().ToArray();

                    // Inicializar el nº de agujeros
                    int numAgujeros = 0;

                    // Analizar cada contorno
                    for (int i = 0; i < contornos.Size; i++)
                    {
                        // Comprobar si el contorno tiene agujeros
                        if (jerarquiaDatos != null)
                        {
                            int padre = jerarquiaDatos[i * 4 + 3]; // Índice del contorno padre

                            // Si este contorno es padre
                            if (padre == -1)
                            {
                                // Contar los contornos de dentro
                                numAgujeros = jerarquiaDatos.Where((p, j) => ((j + 1) % 4 == 0) && p == i).Count();
                            }
                            else
                            {
                                continue; // Si es un contorno hijo, lo ignoramos
                            }
                        }

                        // Obtener el contorno externo i
                        var contorno = contornos[i];

                        // Propiedades del contorno
                        double area = CvInvoke.ContourArea(contorno); // Área del contorno
                        double perimetro = CvInvoke.ArcLength(contorno, true); // Perímetro del contorno
                        Rectangle rectanguloDelimitador = CvInvoke.BoundingRectangle(contorno); // Rectángulo que encierra completamente el contorno

                        double relacionAspecto = (double)rectanguloDelimitador.Width / rectanguloDelimitador.Height; // Relación entre el ancho y el alto del rectángulo delimitador
                        double circularidad = 4 * Math.PI * area / (perimetro * perimetro); // Circularidad = 1 para un círculo perfecto
                        double solidez = area / (rectanguloDelimitador.Width * rectanguloDelimitador.Height); // Solidez = relación entre el área del contorno y el área del rectángulo delimitador

                        // Aproximar el contorno a un polígono
                        double epsilon = 0.02 * perimetro; // Epsilon es un parámetro que controla la precisión de la aproximación
                        var aproximacionPoligono = new VectorOfPoint();
                        CvInvoke.ApproxPolyDP(contorno, aproximacionPoligono, epsilon, true);

                        // Contar el número de vértices del polígono aproximado
                        int numVertices = aproximacionPoligono.Size;

                        // Determinar si el polígono es convexo o cóncavo
                        bool esConvexo = CvInvoke.IsContourConvex(aproximacionPoligono); // true si es convexo (no tiene forma de L)

                        // Determinar el tipo de pieza según las propiedades
                        string tipo = "Pieza desconocida";
                        // string datos = $"Contorno {i}: Área = {area}, Perímetro = {perimetro}, Relación de aspecto = {relacionAspecto}, Circularidad = {circularidad}, Agujeros = {numAgujeros}, Vértices = {numVertices}, Convexa = {esConvexo}, Solidez = {solidez}";
                        // MessageBox.Show(datos, "Datos del contorno");

                        if (circularidad > 0.75 && (relacionAspecto > 0.75 && relacionAspecto < 1.25) && numAgujeros == 1 && numVertices > 7) // Si la circularidad es casi 1, la relación de aspecto es cercana a 1 (cuadrado) y tiene agujero, será una arandela
                        {
                            tipo = "Arandela";
                            arandelas++; // Incrementar el contador de arandelas
                            lblNumArandelasImagen.Text = $"- Arandelas = {arandelas}"; // Actualizar el texto del contador de arandelas
                        }
                        else if (numAgujeros == 1 && (numVertices >= 5 && numVertices <= 7)) // Si tiene 1 agujero y el polígono es un hexágono o heptágono, será una tuerca
                        {
                            tipo = "Tuerca";
                            tuercas++; // Incrementar el contador de tuercas
                            lblNumTuercasImagen.Text = $"- Tuercas = {tuercas}"; // Actualizar el texto del contador de tuercas
                        }
                        else if (numAgujeros > 1 && !esConvexo && area > 14000) // Si tiene varios agujeros, no es convexa y el área es grande, será una placa de unión de soportes en forma de L
                        {
                            tipo = "Placa union soportes en forma de L";
                            placasUnionSoportesL++; // Incrementar el contador de placas de unión de soportes en forma de L
                            lblNumPlacasUnionSoportesLImagen.Text = $"- Placas de unión de soportes en forma de L = {placasUnionSoportesL}"; // Actualizar el texto del contador de placas de unión de soportes en forma de L
                        }
                        else if (numAgujeros > 1 && esConvexo && area > 14000) // Si tiene varios agujeros, es convexa y el área es grande, será una placa de unión de soportes recta
                        {
                            tipo = "Placa union soportes recta";
                            placasUnionSoportesRecta++; // Incrementar el contador de placas de unión de soportes recta
                            lblNumPlacasUnionSoportesRectasImagen.Text = $"- Placas de unión de soportes rectas = {placasUnionSoportesRecta}"; // Actualizar el texto del contador de placas de unión de soportes recta
                        }
                        else if (area < 3000 && circularidad < 0.5) // Si el área es pequeña y no es circular, será un tornillo
                        {
                            tipo = "Tornillo";
                            tornillos++; // Incrementar el contador de tornillos
                            lblNumTornillosImagen.Text = $"- Tornillos = {tornillos}"; // Actualizar el texto del contador de tornillos
                        }
                        else if (numAgujeros == 0 && !esConvexo && area > 5000 && solidez < 0.25) // Si no tiene agujeros, no es convexa, el área es grande y la solidez es muy baja, será una llave Allen
                        {
                            tipo = "Llave Allen";
                            llavesAllen++; // Incrementar el contador de llaves Allen
                            lblNumLlavesAllenImagen.Text = $"- Llaves Allen = {llavesAllen}"; // Actualizar el texto del contador de llaves Allen
                        }
                        else if (!esConvexo && (area > 3000 && area < 5000) && numVertices > 7 && numAgujeros == 0) // Si no es convexa, el área es pequeña, tiene muchos vértices y no tiene agujeros, será un gancho
                        {
                            tipo = "Gancho";
                            ganchos++; // Incrementar el contador de ganchos
                            lblNumGanchosImagen.Text = $"- Ganchos = {ganchos}"; // Actualizar el texto del contador de ganchos
                        }

                        // Dibujar el contorno de la pieza en la imagen original
                        CvInvoke.DrawContours(ImageBGR, contornos, i, new MCvScalar(0, 255, 0), 2); // Dibujar el contorno en verde

                        // Configuración del texto
                        Emgu.CV.CvEnum.FontFace tipoFuente = Emgu.CV.CvEnum.FontFace.HersheySimplex; // Tipo de fuente
                        double escalaFuente = 1.5; // Escala del texto
                        int grosorFuente = 3; // Grosor del texto
                        int baseline = 0; // Variable para almacenar la línea base del texto

                        // Obtener el tamaño del texto
                        Size textSize = CvInvoke.GetTextSize(
                            tipo,
                            tipoFuente,
                            escalaFuente,
                            grosorFuente,
                            ref baseline);

                        // Posición del texto
                        int xTexto = rectanguloDelimitador.X; // Coordenada X
                        int yTexto = rectanguloDelimitador.Y + textSize.Height - 30; // Coordenada Y

                        // Rectángulo de fondo (negro) para el texto
                        var rectanguloFondo = new Rectangle(
                            xTexto, // Coordenada X del rectángulo
                            yTexto - textSize.Height,   // Coordenada Y del rectángulo
                            textSize.Width, // Anchura rectángulo
                            textSize.Height + baseline); // Altura rectángulo

                        // Dibujar un rectángulo de fondo para el texto
                        CvInvoke.Rectangle(
                            ImageBGR, // Imagen sobre la que se dibuja el rectángulo
                            rectanguloFondo, // Rectángulo a dibujar
                            new MCvScalar(0, 0, 0),  // Color negro
                            -1);                     // grosor = -1 → relleno completo

                        // Dibujar el nombre de cada pieza sobre el rectángulo
                        Point textPoint = new Point(xTexto, yTexto);
                        CvInvoke.PutText(
                            ImageBGR, // Imagen sobre la que se dibuja el texto
                            tipo, // Texto a dibujar
                            textPoint, // Punto donde se dibuja el texto
                            Emgu.CV.CvEnum.FontFace.HersheySimplex, // Tipo de fuente
                            escalaFuente, // Escala del texto
                            new MCvScalar(255, 255, 255),  // Color blanco
                            grosorFuente); // Grosor del texto
                    }

                    // Mostrar el resultado en el PictureBox
                    pbtImage.Image?.Dispose();
                    pbtImage.Image = ImageBGR.ToBitmap();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error al identificar piezas: " + ex.Message);
                    MessageBox.Show("Error al identificar piezas: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("No hay imagen cargada para identificar piezas.");
            }
        }

        private void btnConfiguracion_Click(object sender, EventArgs e)
        {
            // Abrir el formulario de configuración de la cámara
            var settingsForm = new CameraSettingsForm(backEnd);
            settingsForm.Show();
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            // Si backEnd está activo, lo detenemos en un hilo aparte
            if (backEnd != null && backEnd.IsActive())
            {
                Task.Run(() =>
                {
                    backEnd.Stop(); // Esto bloquea hasta que se detenga la adquisición
                    // Una vez detenido, cerramos la aplicación desde el hilo de UI
                    this.Invoke(new Action(() =>
                    {
                        // this.Close();
                        Application.Exit(); // Cerrar la aplicación de forma segura
                    }));
                });
            }
            else
            {
                // Si backEnd ya estaba detenido, cerramos inmediatamente
                Application.Exit();
            }
        }

        private SimplePictureBox pbtImage;
        private Button btnCargarImagen;

        private void btnCargarImagen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Cargar imagen desde el archivo seleccionado
                ImageBGR = new Image<Bgr, byte>(openFileDialog.FileName);
                pictureBox.Image = ImageBGR.ToBitmap();

                // Crear contenedor para la imagen en escala de grises
                ImageGray = ImageBGR.Convert<Gray, byte>();

                // Crear contenedor para la imagen binaria
                ImageBin = ImageGray.ThresholdBinaryInv(new Gray(100), new Gray(255));

                // Mostrar la imagen binaria en el segundo PictureBox
                pbtImage.Image = ImageBin.ToBitmap();
            }
            else
            {
                MessageBox.Show("Error al cargar la imagen");
            }
        }

        private Button btnIdentificarPiezasVideo;
        private void btnIdentificarPiezasVideo_Click(object sender, EventArgs e)
        {
            // Inicializar contadores para las piezas
            int tornillos = 0; // Contador de tornillos identificados
            int arandelas = 0; // Contador de arandelas identificadas
            int tuercas = 0; // Contador de tuercas identificadas
            int placasUnionSoportesL = 0; // Contador de placas de unión de soportes en forma de L identificadas
            int placasUnionSoportesRecta = 0; // Contador de placas de unión de soportes recta identificadas
            int llavesAllen = 0; // Contador de llaves Allen identificadas
            int ganchos = 0; // Contador de ganchos identificados

            // Inicializar el texto de los contadores
            lblNumArandelasVideo.Text = $"- Arandelas = {arandelas}"; // Actualizar el texto del contador de arandelas
            lblNumTornillosVideo.Text = $"- Tornillos = {tornillos}"; // Actualizar el texto del contador de tornillos
            lblNumTuercasVideo.Text = $"- Tuercas = {tuercas}"; // Actualizar el texto del contador de tuercas
            lblNumPlacasUnionSoportesLVideo.Text = $"- Placas de unión de soportes en forma de L = {placasUnionSoportesL}"; // Actualizar el texto del contador de placas de unión de soportes en forma de L
            lblNumPlacasUnionSoportesRectasVideo.Text = $"- Placas de unión de soportes rectas = {placasUnionSoportesRecta}"; // Actualizar el texto del contador de placas de unión de soportes rectas
            lblNumLlavesAllenVideo.Text = $"- Llaves Allen = {llavesAllen}"; // Actualizar el texto del contador de llaves Allen
            lblNumGanchosVideo.Text = $"- Ganchos = {ganchos}"; // Actualizar el texto del contador de ganchos

            // Obtener el frame actual del vídeo desde el PictureBox
            try
            {
                if (pictureBox.Image != null)
                {
                    // Convertir la imagen del PictureBox a Bitmap
                    Bitmap bmp = new Bitmap(pictureBox.Image);

                    // Convertir Bitmap a Image<Bgr, byte>
                    ImageBGR = bmp.ToImage<Bgr, byte>();

                    // Crear contenedor para la imagen en escala de grises
                    ImageGray = ImageBGR.Convert<Gray, byte>();

                    // Crear contenedor para la imagen binaria
                    ImageBin = ImageGray.ThresholdBinaryInv(new Gray(100), new Gray(255));
                }
                else
                {
                    MessageBox.Show("No hay imagen en el PictureBox.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al capturar la imagen desde la cámara: " + ex.Message);
            }

            if (pictureBox.Image == null)
            {
                MessageBox.Show("Error: no se está grabando vídeo");
                return;
            }

            // Identificar piezas en el vídeo
            if (pictureBox.Image != null)
            {
                try
                {
                    // Encontrar las piezas (contornos) y la jerarquía en la imagen binaria
                    VectorOfVectorOfPoint contornos = new VectorOfVectorOfPoint();
                    Mat jerarquia = new Mat(); // Matriz para almacenar la jerarquía de los contornos
                    CvInvoke.FindContours(ImageBin, contornos, jerarquia, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

                    // Convertir la jerarquia a una matriz de enteros
                    int[] jerarquiaDatos = jerarquia.GetData(false).Cast<int>().ToArray();

                    // Inicializar el nº de agujeros
                    int numAgujeros = 0;

                    // Analizar cada contorno
                    for (int i = 0; i < contornos.Size; i++)
                    {
                        // Comprobar si el contorno tiene agujeros
                        if (jerarquiaDatos != null)
                        {
                            int padre = jerarquiaDatos[i * 4 + 3]; // Índice del contorno padre

                            // Si este contorno es padre
                            if (padre == -1)
                            {
                                // Contar los contornos de dentro
                                numAgujeros = jerarquiaDatos.Where((p, j) => ((j + 1) % 4 == 0) && p == i).Count();
                            }
                            else
                            {
                                continue; // Si es un contorno hijo, lo ignoramos
                            }
                        }

                        // Obtener el contorno externo i
                        var contorno = contornos[i];

                        // Propiedades del contorno
                        double area = CvInvoke.ContourArea(contorno); // Área del contorno
                        double perimetro = CvInvoke.ArcLength(contorno, true); // Perímetro del contorno
                        Rectangle rectanguloDelimitador = CvInvoke.BoundingRectangle(contorno); // Rectángulo que encierra completamente el contorno

                        double relacionAspecto = (double)rectanguloDelimitador.Width / rectanguloDelimitador.Height; // Relación entre el ancho y el alto del rectángulo delimitador
                        double circularidad = 4 * Math.PI * area / (perimetro * perimetro); // Circularidad = 1 para un círculo perfecto
                        double solidez = area / (rectanguloDelimitador.Width * rectanguloDelimitador.Height); // Solidez = relación entre el área del contorno y el área del rectángulo delimitador

                        // Aproximar el contorno a un polígono
                        double epsilon = 0.02 * perimetro; // Epsilon es un parámetro que controla la precisión de la aproximación
                        var aproximacionPoligono = new VectorOfPoint();
                        CvInvoke.ApproxPolyDP(contorno, aproximacionPoligono, epsilon, true);

                        // Contar el número de vértices del polígono aproximado
                        int numVertices = aproximacionPoligono.Size;

                        // Determinar si el polígono es convexo o cóncavo
                        bool esConvexo = CvInvoke.IsContourConvex(aproximacionPoligono); // true si es convexo (no tiene forma de L)

                        // Determinar el tipo de pieza según las propiedades
                        // string tipo = "Pieza desconocida";
                        // string datos = $"Contorno {i}: Área = {area}, Perímetro = {perimetro}, Relación de aspecto = {relacionAspecto}, Circularidad = {circularidad}, Agujeros = {numAgujeros}, Vértices = {numVertices}, Convexa = {esConvexo}, Solidez = {solidez}";
                        // MessageBox.Show(datos, "Datos del contorno");

                        if (circularidad > 0.75 && (relacionAspecto > 0.75 && relacionAspecto < 1.25) && numAgujeros == 1 && numVertices > 7) // Si la circularidad es casi 1, la relación de aspecto es cercana a 1 (cuadrado) y tiene agujero, será una arandela
                        {
                            arandelas++; // Incrementar el contador de arandelas
                            lblNumArandelasVideo.Text = $"- Arandelas = {arandelas}"; // Actualizar el texto del contador de arandelas
                        }
                        else if (numAgujeros == 1 && (numVertices >= 5 && numVertices <= 7)) // Si tiene 1 agujero y el polígono es un hexágono o heptágono, será una tuerca
                        {
                            tuercas++; // Incrementar el contador de tuercas
                            lblNumTuercasVideo.Text = $"- Tuercas = {tuercas}"; // Actualizar el texto del contador de tuercas
                        }
                        else if (numAgujeros > 1 && !esConvexo && area > 14000) // Si tiene varios agujeros, no es convexa y el área es grande, será una placa de unión de soportes en forma de L
                        {
                            placasUnionSoportesL++; // Incrementar el contador de placas de unión de soportes en forma de L
                            lblNumPlacasUnionSoportesLVideo.Text = $"- Placas de unión de soportes en forma de L = {placasUnionSoportesL}"; // Actualizar el texto del contador de placas de unión de soportes en forma de L
                        }
                        else if (numAgujeros > 1 && esConvexo && area > 14000) // Si tiene varios agujeros, es convexa y el área es grande, será una placa de unión de soportes recta
                        {
                            placasUnionSoportesRecta++; // Incrementar el contador de placas de unión de soportes rectas
                            lblNumPlacasUnionSoportesRectasVideo.Text = $"- Placas de unión de soportes rectas = {placasUnionSoportesRecta}"; // Actualizar el texto del contador de placas de unión de soportes rectas
                        }
                        else if (area < 3000 && circularidad < 0.5) // Si el área es pequeña y no es circular, será un tornillo
                        {
                            tornillos++; // Incrementar el contador de tornillos
                            lblNumTornillosVideo.Text = $"- Tornillos = {tornillos}"; // Actualizar el texto del contador de tornillos
                        }
                        else if (numAgujeros == 0 && !esConvexo && area > 5000 && solidez < 0.25) // Si no tiene agujeros, no es convexa, el área es grande y la solidez es muy baja, será una llave Allen
                        {
                            llavesAllen++; // Incrementar el contador de llaves Allen
                            lblNumLlavesAllenVideo.Text = $"- Llaves Allen = {llavesAllen}"; // Actualizar el texto del contador de llaves Allen
                        }
                        else if (!esConvexo && (area > 3000 && area < 5000) && numVertices > 7 && numAgujeros == 0) // Si no es convexa, el área es pequeña, tiene muchos vértices y no tiene agujeros, será un gancho
                        {
                            ganchos++; // Incrementar el contador de ganchos
                            lblNumGanchosVideo.Text = $"- Ganchos = {ganchos}"; // Actualizar el texto del contador de ganchos
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error al identificar piezas: " + ex.Message);
                    MessageBox.Show("Error al identificar piezas: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("No hay vídeo para identificar piezas.");
            }
        }

        private Label label1;

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private Label label2;
        private GroupBox groupBox1;

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private GroupBox groupBox2;
        private Label lblIdentificarPiezasVideo;
        private Label lblIdentificarPiezasImagen;

        private void lblIdentificarPiezasVideo_Click(object sender, EventArgs e)
        {

        }

        private Label lblNumGanchosVideo;
        private Label lblNumTuercasVideo;
        private Label lblNumPlacasUnionSoportesLVideo;
        private Label lblNumPlacasUnionSoportesRectasVideo;
        private Label lblNumArandelasVideo;
        private Label lblNumTornillosVideo;

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
        private Label lblNumLlavesAllenVideo;

        private void lblNumLlavesAllenVideo_Click(object sender, EventArgs e)
        {

        }

        private Label lblNumLlavesAllenImagen;
        private Label lblNumGanchosImagen;
        private Label lblNumPlacasUnionSoportesRectasImagen;
        private Label lblNumTuercasImagen;
        private Label lblNumTornillosImagen;
        private Label lblNumPlacasUnionSoportesLImagen;
        private Label lblNumArandelasImagen;

        private void FormWindow_Load(object sender, EventArgs e)
        {

        }
        private Label label3;
        private SimplePictureBox pbtVideoTiempoReal;

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void pbtVideoTiempoReal_Click(object sender, EventArgs e)
        {

        }

        private void infoLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
