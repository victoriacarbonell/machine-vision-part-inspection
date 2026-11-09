using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System;

namespace simple_live_windows_forms
{
    class SimplePictureBox : PictureBox
    {
        public new Image Image { 
            get { return base.Image; }
            set { SetImage(value); } 
        }

        private void SetImage(Image image)
        {
            if (mutex.WaitOne(0))
            {
                if (base.Image != null)
                {
                    base.Image.Dispose();
                    base.Image = null;
                }

                base.Image = image;
                mutex.ReleaseMutex();
            }
            else
            {
                image.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            mutex.WaitOne();
            base.OnPaint(pe);
            mutex.ReleaseMutex();
        }

        private Mutex mutex = new Mutex();
    }
}
