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

namespace simple_live_windows_forms
{
    public class CameraSettingsForm : Form
    {
        private TrackBar trbExposicion;
        private Label lblExposicion;
        private TrackBar trbGanancia;
        private Label lblGanancia;
        // private CheckBox chkBlancoNegro;
        private Label lblModoColor;
        private TrackBar trbFPS;
        private Label lblFPS;
        private Button btnCerrar;

        private BackEnd backEnd;

        // Constructor que recibe el BackEnd para interactuar con la cámara
        public CameraSettingsForm(BackEnd backEnd)
        {
            this.backEnd = backEnd;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Tamaño y título del formulario
            this.Text = "Configuración de Cámara";
            this.ClientSize = new Size(300, 300);

            // TrackBar de Exposición
            lblExposicion = new Label
            {
                Text = "Exposición (ms):",
                Location = new Point(10, 10),
                AutoSize = true
            };
            trbExposicion = new TrackBar
            {
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                TickFrequency = 10,
                Location = new Point(10, 30),
                Width = 260
            };
            trbExposicion.Scroll += TrbExposicion_Scroll;

            // TrackBar de Ganancia
            lblGanancia = new Label
            {
                Text = "Ganancia:",
                Location = new Point(10, 110),
                AutoSize = true
            };
            trbGanancia = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickFrequency = 10,
                Location = new Point(10, 130),
                Width = 260
            };
            trbGanancia.Scroll += TrbGanancia_Scroll;

            /*
            // CheckBox Blanco/Negro
            lblModoColor = new Label
            {
                Text = "Modo:",
                Location = new Point(10, 140),
                AutoSize = true
            };
            chkBlancoNegro = new CheckBox
            {
                Text = "Blanco y Negro",
                Location = new Point(60, 138),
                AutoSize = true
            };
            chkBlancoNegro.CheckedChanged += ChkBlancoNegro_CheckedChanged;
            */

            // TrackBar de FPS
            lblFPS = new Label
            {
                Text = "FPS:",
                Location = new Point(10, 210),
                AutoSize = true
            };
            trbFPS = new TrackBar
            {
                Minimum = 1,
                Maximum = 60,
                Value = 30,
                TickFrequency = 5,
                Location = new Point(10, 230),
                Width = 260
            };
            trbFPS.Scroll += TrbFPS_Scroll;

            // Botón Cerrar
            btnCerrar = new Button
            {
                Text = "Cerrar",
                Location = new Point(100, 300),
                Size = new Size(80, 30)
            };
            btnCerrar.Click += BtnCerrar_Click;

            // Añadimos controles al formulario
            this.Controls.Add(lblExposicion);
            this.Controls.Add(trbExposicion);
            this.Controls.Add(lblGanancia);
            this.Controls.Add(trbGanancia);
            this.Controls.Add(lblModoColor);
            // this.Controls.Add(chkBlancoNegro);
            this.Controls.Add(lblFPS);
            this.Controls.Add(trbFPS);
            this.Controls.Add(btnCerrar);
        }

        // Evento Exposición
        private void TrbExposicion_Scroll(object sender, EventArgs e)
        {
            double valorMs = trbExposicion.Value;
            backEnd.SetExposure(valorMs);
            lblExposicion.Text = $"Exposición (ms): {valorMs}";
        }

        // Evento Ganancia
        private void TrbGanancia_Scroll(object sender, EventArgs e)
        {
            double valor = trbGanancia.Value / 10.0;
            backEnd.SetGain(valor);
            lblGanancia.Text = $"Ganancia: {valor}";
        }

        /*
        // Evento Modo Blanco/Negro
        private void ChkBlancoNegro_CheckedChanged(object sender, EventArgs e)
        {
            bool bn = chkBlancoNegro.Checked;
            backEnd.SetBlackAndWhite(bn);
            lblModoColor.Text = bn ? "Modo: B/N" : "Modo: Color";
        }
        */

        // Evento FPS
        private void TrbFPS_Scroll(object sender, EventArgs e)
        {
            double valorFps = trbFPS.Value;
            backEnd.SetFPS(valorFps);
            lblFPS.Text = $"FPS: {valorFps}";
        }

        // Botón Cerrar
        private void BtnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
