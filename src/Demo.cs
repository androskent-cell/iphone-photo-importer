using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImportarIPhone
{
    // Modo demo (--demo): llena la app con fotos inventadas (paisajes dibujados) para hacer capturas
    // de pantalla sin mostrar fotos reales de nadie. No lee ni copia nada del iPhone.
    public static class Demo
    {
        public static bool Activo;

        public static List<MonthSection> Secciones()
        {
            var r = new Random(7);
            var res = new List<MonthSection>();
            var hoy = DateTime.Now;
            int[] cantidades = { 46, 63, 38, 55, 31, 49 };
            double[] yaEnPC = { 0.0, 0.3, 1.0, 1.0, 1.0, 1.0 };
            int numero = 5200;
            for (int k = 0; k < cantidades.Length; k++)
            {
                var mes = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-k);
                var sec = new MonthSection { Key = mes.ToString("yyyy-MM") };
                var fecha = (k == 0 ? hoy.AddHours(-2) : mes.AddMonths(1).AddHours(-4)).ToUniversalTime();
                for (int i = 0; i < cantidades[k]; i++)
                {
                    fecha = fecha.AddMinutes(-r.Next(40, 900));
                    bool video = r.NextDouble() < 0.18;
                    string nombre = "IMG_" + (numero--);
                    var m = new MediaItem
                    {
                        Name = nombre + (video ? ".MOV" : ".HEIC"), Ext = video ? ".MOV" : ".HEIC",
                        Size = video ? r.Next(18, 950) * 1024L * 1024 : r.Next(1400, 4600) * 1024L,
                        DateUtc = fecha, Month = sec.Key, IsVideo = video
                    };
                    if (!video && r.NextDouble() < 0.45)
                        m.Companion = new MediaItem { Name = nombre + ".MOV", Ext = ".MOV", Size = r.Next(2, 7) * 1024L * 1024, DateUtc = fecha, Month = sec.Key, IsVideo = true };
                    m.Imported = r.NextDouble() < yaEnPC[k];
                    sec.Items.Add(m);
                }
                res.Add(sec);
            }
            foreach (int i in new[] { 1, 2, 4, 5, 9, 10 }) res[0].Items[i].Selected = true;
            return res;
        }

        // Colores: cielo arriba, cielo en el horizonte, sol, montaña lejana, montaña cercana
        static readonly Color[][] Paletas =
        {
            new[] { Color.FromArgb(64, 59, 122), Color.FromArgb(255, 145, 96), Color.FromArgb(255, 226, 150), Color.FromArgb(122, 78, 110), Color.FromArgb(52, 36, 64) },
            new[] { Color.FromArgb(70, 140, 220), Color.FromArgb(190, 225, 245), Color.FromArgb(255, 250, 220), Color.FromArgb(118, 150, 172), Color.FromArgb(58, 96, 82) },
            new[] { Color.FromArgb(118, 108, 182), Color.FromArgb(250, 190, 188), Color.FromArgb(255, 240, 210), Color.FromArgb(150, 120, 160), Color.FromArgb(82, 70, 112) },
            new[] { Color.FromArgb(20, 80, 112), Color.FromArgb(120, 200, 200), Color.FromArgb(240, 255, 240), Color.FromArgb(42, 112, 122), Color.FromArgb(16, 62, 72) },
            new[] { Color.FromArgb(236, 160, 72), Color.FromArgb(255, 226, 150), Color.FromArgb(255, 250, 230), Color.FromArgb(188, 118, 60), Color.FromArgb(106, 58, 40) },
            new[] { Color.FromArgb(40, 120, 190), Color.FromArgb(150, 210, 240), Color.FromArgb(255, 255, 235), Color.FromArgb(70, 140, 110), Color.FromArgb(40, 95, 60) },
        };

        public static Bitmap Imagen(MediaItem m, int px)
        {
            var r = new Random(m.Id * 7919 + 13);
            var b = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var p = Paletas[r.Next(Paletas.Length)];
                var todo = new Rectangle(0, 0, px, px);
                int escena = r.Next(5);
                Color arriba = p[0], horizonte = p[1];
                if (escena == 2) { arriba = Color.FromArgb(14, 18, 44); horizonte = Color.FromArgb(70, 54, 110); }
                using (var cielo = new LinearGradientBrush(todo, arriba, horizonte, 90f)) g.FillRectangle(cielo, todo);
                float h = px * (0.5f + (float)r.NextDouble() * 0.2f);   // altura del horizonte

                switch (escena)
                {
                    case 0:   // montañas con sol
                    case 4:
                        Sol(g, r, p[2], px, h);
                        Montanas(g, r, p[3], px, h - px * 0.12f, px * 0.22f);
                        Montanas(g, r, p[4], px, h + px * 0.05f, px * 0.18f);
                        break;
                    case 1:   // playa
                        Sol(g, r, p[2], px, h);
                        using (var mar = new LinearGradientBrush(new RectangleF(0, h, px, px - h + 1), p[3], p[4], 90f))
                            g.FillRectangle(mar, 0, h, px, px - h);
                        using (var arena = new SolidBrush(Color.FromArgb(238, 218, 178)))
                            g.FillEllipse(arena, -px * 0.3f, h + (px - h) * 0.55f, px * 1.6f, px);
                        break;
                    case 2:   // ciudad de noche
                        float x = 0;
                        while (x < px)
                        {
                            float w = px * (0.08f + (float)r.NextDouble() * 0.12f), alto = px * (0.2f + (float)r.NextDouble() * 0.45f);
                            using (var ed = new SolidBrush(Color.FromArgb(20 + r.Next(25), 22 + r.Next(25), 40 + r.Next(30)))) g.FillRectangle(ed, x, px - alto, w - 1, alto);
                            using (var luz = new SolidBrush(Color.FromArgb(230, 255, 214, 120)))
                                for (float vy = px - alto + 4; vy < px - 4; vy += px * 0.045f)
                                    for (float vx = x + 3; vx < x + w - 5; vx += px * 0.04f)
                                        if (r.NextDouble() < 0.45) g.FillRectangle(luz, vx, vy, px * 0.018f, px * 0.022f);
                            x += w;
                        }
                        break;
                    case 3:   // luces desenfocadas
                        for (int i = 0; i < 14; i++)
                        {
                            float d = px * (0.1f + (float)r.NextDouble() * 0.3f);
                            var c = Color.FromArgb(50 + r.Next(80), p[2].R, Math.Max(0, p[2].G - r.Next(40)), Math.Max(0, p[2].B - r.Next(60)));
                            using (var br = new SolidBrush(c)) g.FillEllipse(br, (float)r.NextDouble() * px - d / 2, (float)r.NextDouble() * px - d / 2, d, d);
                        }
                        break;
                }
            }
            return b;
        }

        static void Sol(Graphics g, Random r, Color c, int px, float h)
        {
            float d = px * (0.14f + (float)r.NextDouble() * 0.1f);
            float x = px * (0.15f + (float)r.NextDouble() * 0.6f), y = h - d * (0.8f + (float)r.NextDouble() * 1.4f);
            using (var halo = new SolidBrush(Color.FromArgb(60, c))) g.FillEllipse(halo, x - d * 0.4f, y - d * 0.4f, d * 1.8f, d * 1.8f);
            using (var br = new SolidBrush(c)) g.FillEllipse(br, x, y, d, d);
        }

        static void Montanas(Graphics g, Random r, Color c, int px, float base0, float alto)
        {
            var pts = new List<PointF> { new PointF(0, px) };
            float x = 0;
            while (x <= px)
            {
                pts.Add(new PointF(x, base0 - (float)r.NextDouble() * alto));
                x += px * (0.12f + (float)r.NextDouble() * 0.18f);
            }
            pts.Add(new PointF(px, base0 - (float)r.NextDouble() * alto));
            pts.Add(new PointF(px, px));
            using (var br = new SolidBrush(c)) g.FillPolygon(br, pts.ToArray());
        }
    }
}
