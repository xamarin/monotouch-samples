using System;
using OpenTK.Platform.iPhoneOS;
using OpenTK.Graphics.ES11;
using UIKit;
using Foundation;

using ObjCRuntime;
using OpenGLES;
using System.Runtime.InteropServices;
using CoreGraphics;

namespace GLPaintGameView {
	public class PaintingView : iPhoneOSGameView {

		public const float BrushOpacity = 1.0f / 3.0f;
		public const int BrushPixelStep = 3;
		public const int BrushScale = 2;
		public const float Luminosity = 0.75f;
		public const float Saturation = 1.0f;

		uint brushTexture, drawingTexture;
		bool firstTouch;

		CGPoint Location;
		CGPoint PreviousLocation;

		[Export ("layerClass")]
		public static Class LayerClass ()
		{
			return iPhoneOSGameView.GetLayerClass ();
		}

		public PaintingView (CGRect frame)
			: base (frame)
		{
			LayerRetainsBacking = true;
			LayerColorFormat = EAGLColorFormat.RGBA8;
			ContextRenderingApi = EAGLRenderingAPI.OpenGLES1;
			CreateFrameBuffer ();
			MakeCurrent ();

			var brushImage = UIImage.FromFile ("Particle.png").CGImage;
			var width = brushImage.Width;
			var height = brushImage.Height;
			if (brushImage != null) {
				IntPtr brushData = Marshal.AllocHGlobal ((int) (width * height * 4));
				if (brushData == IntPtr.Zero)
					throw new OutOfMemoryException ();
				try {
					using (var brushContext = new CGBitmapContext (brushData,
												  width, width, 8, width * 4, brushImage.ColorSpace, CGImageAlphaInfo.PremultipliedLast)) {
						brushContext.DrawImage (new CGRect (0.0f, 0.0f, (float) width, (float) height), brushImage);
					}

					GL.GenTextures (1, out brushTexture);
					GL.BindTexture (All.Texture2D, brushTexture);
					GL.TexImage2D (All.Texture2D, 0, (int) All.Rgba, (int) width, (int) height, 0, All.Rgba, All.UnsignedByte, brushData);
				} finally {
					Marshal.FreeHGlobal (brushData);
				}
				GL.TexParameter (All.Texture2D, All.TextureMinFilter, (int) All.Linear);
				GL.Enable (All.Texture2D);
				GL.BlendFunc (All.SrcAlpha, All.One);
				GL.Enable (All.Blend);
			}
			GL.Disable (All.Dither);
			GL.MatrixMode (All.Projection);
			GL.Ortho (0, (int) frame.Width, 0, (int) frame.Height, -1, 1);
			GL.MatrixMode (All.Modelview);
			GL.Enable (All.Texture2D);
			GL.EnableClientState (All.VertexArray);
			GL.Enable (All.Blend);
			GL.BlendFunc (All.SrcAlpha, All.One);
			GL.Enable (All.PointSpriteOes);
			GL.TexEnv (All.PointSpriteOes, All.CoordReplaceOes, (float) All.True);
			GL.PointSize (width / BrushScale);

			Erase ();

			PerformSelector (new Selector ("playback"), null, 0.2f);
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			GL.DeleteTextures (1, ref drawingTexture);
		}

		public void Erase ()
		{
			GL.Clear (ClearBufferMask.ColorBufferBit);

			SwapBuffers ();
		}

		float [] vertexBuffer;
		int vertexMax = 64;

		private void RenderLineFromPoint (CGPoint start, CGPoint end)
		{
			int vertexCount = 0;
			if (vertexBuffer == null) {
				vertexBuffer = new float [vertexMax * 2];
			}
			var count = Math.Max (Math.Ceiling (Math.Sqrt ((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y)) / BrushPixelStep),
							1);
			for (int i = 0; i < count; ++i, ++vertexCount) {
				if (vertexCount == vertexMax) {
					vertexMax *= 2;
					Array.Resize (ref vertexBuffer, vertexMax * 2);
				}
				vertexBuffer [2 * vertexCount + 0] = (float) (start.X + (end.X - start.X) * (float) i / (float) count);
				vertexBuffer [2 * vertexCount + 1] = (float) (start.Y + (end.Y - start.Y) * (float) i / (float) count);
			}
			GL.VertexPointer (2, All.Float, 0, vertexBuffer);
			GL.DrawArrays (All.Points, 0, vertexCount);

			SwapBuffers ();
		}

		int dataofs = 0;

		[Export ("playback")]
		void Playback ()
		{
			CGPoint [] points = ShakeMe.Data [dataofs];

			for (int i = 0; i < points.Length - 1; i++)
				RenderLineFromPoint (points [i], points [i + 1]);

			if (dataofs < ShakeMe.Data.Count - 1) {
				dataofs++;
				PerformSelector (new Selector ("playback"), null, 0.01f);
			}
		}

		public override void TouchesBegan (NSSet touches, UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;
			firstTouch = true;
			Location = touch.LocationInView (this);
			Location.Y = bounds.Height - Location.Y;
		}

		public override void TouchesMoved (NSSet touches, UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;

			if (firstTouch) {
				firstTouch = false;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
			} else {
				Location = touch.LocationInView (this);
				Location.Y = bounds.Height - Location.Y;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
			}
			RenderLineFromPoint (PreviousLocation, Location);
		}

		public override void TouchesEnded (NSSet touches, UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;
			if (firstTouch) {
				firstTouch = false;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
				RenderLineFromPoint (PreviousLocation, Location);
			}
		}

		public override void TouchesCancelled (NSSet touches, UIEvent e)
		{
		}
	}
}

