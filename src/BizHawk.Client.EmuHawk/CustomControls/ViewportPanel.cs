using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// A programmatic PictureBox, really, which will paint itself using the last bitmap that was provided
	/// </summary>
	public class RetainedViewportPanel : Control
	{
		private Thread threadPaint;
		private EventWaitHandle ewh;
		private volatile bool killSignal;

		public Func<Bitmap,bool> ReleaseCallback;

		/// <summary>
		/// Turns this panel into multi-threaded mode.
		/// This will sort of glitch out other gdi things on the system, but at least its fast...
		/// </summary>
		public void ActivateThreaded()
		{
			ewh = new EventWaitHandle(false, EventResetMode.AutoReset);
			threadPaint = new Thread(PaintProc) { IsBackground = true };
			threadPaint.Start();
		}

		public RetainedViewportPanel(bool doubleBuffer = false)
		{
			CreateHandle();
			
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.DoubleBuffer, doubleBuffer);
			SetStyle(ControlStyles.Opaque, true);
			SetStyle(ControlStyles.UserMouse, true);

			SetBitmap(new Bitmap(2, 2));
		}


		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (threadPaint != null)
			{
				killSignal = true;
				ewh.Set();
				ewh.WaitOne();
			}
			CleanupDisposeQueue();
		}

		public bool ScaleImage = true;

		private void DoPaint()
		{
			if (bmp != null)
			{
				using Graphics g = CreateGraphics();
				g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
				g.CompositingMode = CompositingMode.SourceCopy;
				g.CompositingQuality = CompositingQuality.HighSpeed;
				if (ScaleImage)
				{
					g.InterpolationMode = InterpolationMode.NearestNeighbor;
					g.PixelOffsetMode = PixelOffsetMode.Half;
					g.DrawImage(bmp, 0, 0, Width, Height);
				}
				else
				{
					using (var sb = new SolidBrush(Color.Black))
					{
						g.FillRectangle(sb, bmp.Width, 0, Width - bmp.Width, Height);
						g.FillRectangle(sb, 0, bmp.Height, bmp.Width, Height - bmp.Height);
					}
					g.DrawImageUnscaled(bmp, 0, 0);
				}
			}

			CleanupDisposeQueue();
		}

		private void PaintProc()
		{
			for (; ; )
			{
				ewh.WaitOne();
				if (killSignal)
				{
					ewh.Set();
					return;
				}

				DoPaint();
			}
		}

		private void CleanupDisposeQueue()
		{
			lock (this)
			{
				while (DisposeQueue.Count > 0)
				{
					var bmp = DisposeQueue.Dequeue();
					bool dispose = true;
					if(ReleaseCallback != null)
						dispose = ReleaseCallback(bmp);
					if(dispose) bmp.Dispose();
				}
			}
		}

		private Queue<Bitmap> DisposeQueue = new Queue<Bitmap>();

		private void SignalPaint()
		{
			if (threadPaint == null)
				DoPaint();
			else
				ewh.Set();
		}

		/// <summary>
		/// Takes ownership of the provided bitmap and will use it for future painting
		/// </summary>
		public void SetBitmap(Bitmap newbmp)
		{
			lock (this)
			{
				if(bmp != null) DisposeQueue.Enqueue(bmp);
				bmp = newbmp;
			}
			SignalPaint();
		}

		private Bitmap bmp;

		/// <summary>bit of a hack; use at your own risk</summary>
		/// <returns>you probably shouldn't modify this?</returns>
		public Bitmap GetBitmap()
		{
			return bmp;
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
		}


		protected override void OnPaint(PaintEventArgs e)
		{
			SignalPaint();
			base.OnPaint(e);
		}
	}
}