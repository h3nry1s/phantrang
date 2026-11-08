using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace PhanTrang
{
    /// <summary>A picture placed on a board sheet (a diagram, a photo of the textbook…). It can be moved, turned and resized.</summary>
    public class PictureMark : Mark
    {
        public const int MaxSide = 1600;                 // pixels kept: plenty for a projector, keeps lessons small
        public const long MaxFileBytes = 30L << 20;      // refuse bigger files outright
        public Bitmap Img;
        public byte[] Data;                              // the encoded picture, as stored in the lesson file
        PointF center;
        SizeF size;
        float angle;                                     // degrees, clockwise

        public PointF Center { get { return center; } set { center = value; Reframe(); } }
        public SizeF Size { get { return size; } set { size = value; Reframe(); } }
        public float Angle { get { return angle; } set { angle = (float)(((value % 360) + 360) % 360); Reframe(); } }

        /// <summary>The picture before rotation (centre and size); the angle is kept separately.</summary>
        public RectangleF Rect
        {
            get { return new RectangleF(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height); }
            set { center = new PointF(value.X + value.Width / 2, value.Y + value.Height / 2); size = value.Size; Reframe(); }
        }

        /// <summary>The four corners on screen, clockwise from top-left.</summary>
        public PointF[] Corners()
        {
            float hw = size.Width / 2, hh = size.Height / 2;
            return new[] { ToScreen(new PointF(-hw, -hh)), ToScreen(new PointF(hw, -hh)), ToScreen(new PointF(hw, hh)), ToScreen(new PointF(-hw, hh)) };
        }

        /// <summary>A screen point in the picture's own unrotated frame (0,0 = its centre).</summary>
        public PointF ToLocal(PointF p)
        {
            double rad = -angle * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
            double dx = p.X - center.X, dy = p.Y - center.Y;
            return new PointF((float)(dx * cos - dy * sin), (float)(dx * sin + dy * cos));
        }

        public PointF ToScreen(PointF local)
        {
            double rad = angle * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
            return new PointF((float)(center.X + local.X * cos - local.Y * sin), (float)(center.Y + local.X * sin + local.Y * cos));
        }

        void Reframe() { Bounds = Geo.Box(Corners(), 8); }

        public override void Draw(Graphics g, double now)
        {
            if (Img == null) return;
            var saved = g.Save();
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(angle);
            var r = new RectangleF(-size.Width / 2, -size.Height / 2, size.Width, size.Height);
            using (var shade = new SolidBrush(Color.FromArgb((int)(50 * Alpha), 0, 0, 0)))
                g.FillRectangle(shade, r.X + 3, r.Y + 5, r.Width, r.Height);
            if (Alpha >= 0.999f) g.DrawImage(Img, r);
            else
                using (var fade = new ImageAttributes())
                {
                    fade.SetColorMatrix(new ColorMatrix { Matrix33 = Alpha });
                    var corners = new[] { r.Location, new PointF(r.Right, r.Top), new PointF(r.Left, r.Bottom) };
                    g.DrawImage(Img, corners, new RectangleF(0, 0, Img.Width, Img.Height), GraphicsUnit.Pixel, fade);
                }
            g.Restore(saved);
        }

        public override bool Hit(PointF p, float r)
        {
            var l = ToLocal(p);
            return Math.Abs(l.X) <= size.Width / 2 + r && Math.Abs(l.Y) <= size.Height / 2 + r;
        }

        /// <summary>Fit the picture inside maxW x maxH (never enlarged past 1.5x), centred on c.</summary>
        public void PlaceAt(PointF c, float maxW, float maxH)
        {
            float k = Math.Min(1.5f, Math.Min(maxW / Img.Width, maxH / Img.Height));
            float w = Img.Width * k, h = Img.Height * k;
            Rect = new RectangleF(c.X - w / 2, c.Y - h / 2, w, h);
        }

        enum Kind { None, Png, Jpeg, Bmp, Gif }

        static Kind Sniff(byte[] b)
        {
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return Kind.Png;
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return Kind.Jpeg;
            if (b[0] == 0x42 && b[1] == 0x4D) return Kind.Bmp;
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return Kind.Gif;
            return Kind.None;
        }

        /// <summary>
        /// Decodes untrusted bytes (a file the teacher picked, or a lesson someone sent). Only PNG, JPEG, BMP and GIF are
        /// accepted, by their signature rather than their name. The pixels are redrawn into a fresh bitmap no larger
        /// than MaxSide and re-encoded, which also drops camera metadata such as GPS position.
        /// fromLesson: the bytes come from a saved lesson, i.e. they were cleaned when first added. If they need no
        /// shrinking they are kept as they are, so opening and saving a lesson again and again never wears a photo down.
        /// </summary>
        public static PictureMark FromBytes(byte[] bytes, bool fromLesson = false)
        {
            if (bytes == null || bytes.Length < 8 || bytes.Length > MaxFileBytes) throw new InvalidDataException("Ảnh quá lớn hoặc bị hỏng.");
            var kind = Sniff(bytes);
            if (kind == Kind.None) throw new InvalidDataException("Chỉ nhận ảnh PNG, JPG, BMP hoặc GIF.");
            Bitmap clean;
            bool shrunk;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var src = Image.FromStream(ms, false, true))
                {
                    if (src.Width < 1 || src.Height < 1 || (long)src.Width * src.Height > 120000000L) throw new InvalidDataException("Ảnh có kích thước không hợp lệ.");
                    double k = Math.Min(1.0, MaxSide / (double)Math.Max(src.Width, src.Height));
                    shrunk = k < 1;
                    int w = Math.Max(1, (int)Math.Round(src.Width * k)), h = Math.Max(1, (int)Math.Round(src.Height * k));
                    clean = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(clean))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception e) { throw new InvalidDataException("Không đọc được ảnh này.", e); }
            bool keep = fromLesson && !shrunk && (kind == Kind.Png || kind == Kind.Jpeg);
            return new PictureMark { Img = clean, Data = keep ? (byte[])bytes.Clone() : Encode(clean, kind == Kind.Png || kind == Kind.Gif) };
        }

        /// <summary>Photos as JPEG (small), pictures that may be transparent as PNG.</summary>
        static byte[] Encode(Bitmap b, bool keepAlpha)
        {
            using (var ms = new MemoryStream())
            {
                if (keepAlpha) b.Save(ms, ImageFormat.Png);
                else
                {
                    var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (var q = new EncoderParameters(1))
                    {
                        q.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                        b.Save(ms, jpeg, q);
                    }
                }
                return ms.ToArray();
            }
        }
    }
}
