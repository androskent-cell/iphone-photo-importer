using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace ImportarIPhone
{
    // La cuadrícula de fotos, agrupada por mes. Solo dibuja lo que está en pantalla.
    public class Cuadricula : Panel
    {
        public List<MonthSection> Secciones = new List<MonthSection>();
        public Func<MediaItem, Bitmap> Miniatura;
        public Action<List<MediaItem>> PedirMiniaturas;
        public event EventHandler SeleccionCambio;
        public event Action<MediaItem> Abrir;
        public event EventHandler Desplazado;
        public string TextoVacio = "";
        public bool Bloqueada;   // mientras copia no se cambia la selección
        public static bool Privado;   // pruebas: no dibuja el contenido de las fotos

        public static readonly Color Acento = Color.FromArgb(0, 122, 255);
        public static readonly Color ColorTexto = Color.FromArgb(29, 29, 31);
        public static readonly Color ColorSecundario = Color.FromArgb(110, 110, 115);
        static readonly Color ColorVacio = Color.FromArgb(236, 236, 240);
        static readonly Color Verde = Color.FromArgb(36, 160, 70);

        float s = 1f;
        int tileBase, tile, gap, cab, margen, columnas = 1;
        MediaItem hover, ancla;
        MonthSection hoverLink;
        bool acomodando;
        Font fTitulo, fSub, fPill, fVacio, fExt;

        public Cuadricula()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            AutoScroll = true;
            BackColor = Color.White;
            Escala(1f);
        }

        public int TamanoMiniatura { get { return (int)(tileBase * 1.15f); } }

        public void Escala(float escala)
        {
            s = escala;
            tileBase = tile = (int)(132 * s); gap = (int)(6 * s); cab = (int)(56 * s); margen = (int)(22 * s);
            fTitulo = new Font("Segoe UI Semibold", 13f);
            fSub = new Font("Segoe UI", 9.75f);
            fPill = new Font("Segoe UI Semibold", 7.5f);
            fVacio = new Font("Segoe UI", 13f);
            fExt = new Font("Segoe UI Semibold", 9f);
        }

        public void Reacomodar()
        {
            if (acomodando) return;
            acomodando = true;
            try
            {
                // Las fotos se estiran un poco para llenar todo el ancho
                int ancho = ClientSize.Width - 2 * margen;
                columnas = Math.Max(1, (ancho + gap) / (tileBase + gap));
                tile = Math.Max(tileBase, (ancho - (columnas - 1) * gap) / columnas);
                int y = (int)(6 * s);
                foreach (var sec in Secciones)
                {
                    sec.Top = y;
                    if (sec.View.Count == 0) { sec.Height = 0; continue; }
                    int filas = (sec.View.Count + columnas - 1) / columnas;
                    sec.Height = cab + filas * (tile + gap);
                    y += sec.Height + (int)(12 * s);
                }
                AutoScrollMinSize = new Size(0, y + margen);
            }
            finally { acomodando = false; }
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Reacomodar(); }

        public void IrA(MonthSection sec)
        {
            AutoScrollPosition = new Point(0, sec.Top);
            Invalidate();
        }

        public MonthSection SeccionArriba()
        {
            int y = -AutoScrollPosition.Y + cab;
            MonthSection res = null;
            foreach (var sec in Secciones)
            {
                if (sec.View.Count == 0) continue;
                if (res == null || sec.Top <= y) res = sec; else break;
            }
            return res;
        }

        // ------------------------------------------------------------ dibujo

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int dy = AutoScrollPosition.Y;
            int arriba = -dy, abajo = -dy + ClientSize.Height;
            int paso = tile + gap;
            var faltan = new List<MediaItem>();
            bool alguna = false;

            foreach (var sec in Secciones)
            {
                if (sec.View.Count == 0) continue;
                alguna = true;
                if (sec.Top + sec.Height < arriba - paso || sec.Top > abajo + paso) { continue; }
                DibujarCabecera(g, sec, dy);
                int y0 = sec.Top + cab;
                int f1 = Math.Max(0, (arriba - y0) / paso - 1);
                int f2 = (abajo - y0) / paso + 1;   // una fila extra abajo para que ya esté lista al bajar
                for (int f = f1; f <= f2; f++)
                    for (int c = 0; c < columnas; c++)
                    {
                        int i = f * columnas + c;
                        if (i < 0 || i >= sec.View.Count) continue;
                        var r = new Rectangle(margen + c * paso, y0 + f * paso + dy, tile, tile);
                        DibujarFoto(g, sec.View[i], r, faltan);
                    }
            }

            if (!alguna && TextoVacio.Length > 0)
            {
                var r = new Rectangle(margen, 0, ClientSize.Width - 2 * margen, ClientSize.Height);
                TextRenderer.DrawText(g, TextoVacio, fVacio, r, ColorSecundario,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            if (PedirMiniaturas != null) PedirMiniaturas(faltan);
        }

        void DibujarCabecera(Graphics g, MonthSection sec, int dy)
        {
            int y = sec.Top + dy + (int)(18 * s);
            var pt = new Point(margen - (int)(2 * s), y);
            TextRenderer.DrawText(g, sec.Title, fTitulo, pt, ColorTexto, TextFormatFlags.NoPadding);
            var tamTitulo = TextRenderer.MeasureText(g, sec.Title, fTitulo, Size.Empty, TextFormatFlags.NoPadding);

            int nuevos = 0, seleccionadosNuevos = 0, seleccionados = 0;
            foreach (var m in sec.View)
            {
                if (!m.Imported) { nuevos++; if (m.Selected) seleccionadosNuevos++; }
                if (m.Selected) seleccionados++;
            }
            string sub = Formato.Numero(sec.View.Count) + (sec.View.Count == 1 ? L.T(" elemento", " item") : L.T(" elementos", " items")) +
                         (nuevos > 0 ? "  ·  " + Formato.Numero(nuevos) + (nuevos == 1 ? L.T(" nuevo", " new") : L.T(" nuevos", " new"))
                                     : "  ·  " + L.T("todo está en la PC", "all on this PC"));
            var tamSub = TextRenderer.MeasureText(g, sub, fSub, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, sub, fSub, new Point(pt.X + tamTitulo.Width + (int)(12 * s), y + tamTitulo.Height - tamSub.Height - (int)(2 * s)),
                                  ColorSecundario, TextFormatFlags.NoPadding);

            string link = null;
            if (nuevos > 0 && seleccionadosNuevos < nuevos) link = L.T("Seleccionar los nuevos", "Select new");
            else if (seleccionados > 0) link = L.T("Quitar selección", "Clear selection");
            sec.LinkRect = Rectangle.Empty;
            if (link != null && !Bloqueada)
            {
                var fl = hoverLink == sec ? new Font(fSub, FontStyle.Underline) : fSub;
                var tl = TextRenderer.MeasureText(g, link, fl, Size.Empty, TextFormatFlags.NoPadding);
                int derecha = margen + columnas * (tile + gap) - gap;
                var r = new Rectangle(derecha - tl.Width, y + tamTitulo.Height - tl.Height - (int)(2 * s), tl.Width, tl.Height);
                TextRenderer.DrawText(g, link, fl, r.Location, Acento, TextFormatFlags.NoPadding);
                if (fl != fSub) fl.Dispose();
                r.Inflate((int)(6 * s), (int)(6 * s));
                r.Offset(0, -dy);
                sec.LinkRect = r;
            }
        }

        void DibujarFoto(Graphics g, MediaItem m, Rectangle r, List<MediaItem> faltan)
        {
            Bitmap b = Miniatura != null ? Miniatura(m) : null;
            if (b != null && Privado)
            {
                // Modo de prueba: solo el color promedio, para no guardar imágenes de las fotos
                Color promedio;
                using (var uno = new Bitmap(b, 1, 1)) promedio = uno.GetPixel(0, 0);
                using (var br = new SolidBrush(Color.FromArgb(255, promedio))) g.FillRectangle(br, r);
            }
            else if (b != null) g.DrawImage(b, r);
            else
            {
                using (var br = new SolidBrush(ColorVacio)) g.FillRectangle(br, r);
                TextRenderer.DrawText(g, m.Ext.TrimStart('.'), fExt, r, Color.FromArgb(170, 170, 178),
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                faltan.Add(m);
            }

            if (m.Imported && !m.Selected)
                using (var br = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(br, r);

            int p = (int)(6 * s);
            if (m.Companion != null) Pastilla(g, "LIVE", r.Left + p, r.Top + p, false, Color.FromArgb(140, 0, 0, 0));
            if (m.IsVideo) Pastilla(g, "▶  " + Formato.Tamano(m.Size), r.Left + p, r.Bottom - p, true, Color.FromArgb(160, 0, 0, 0));
            if (m.Imported)
            {
                string enPC = L.T("✓ En la PC", "✓ On PC");
                var tam = Medir(g, enPC);
                Pastilla(g, enPC, r.Right - p - tam.Width, r.Bottom - p, true, Verde);
            }

            if (m.Selected)
            {
                using (var pen = new Pen(Acento, 3 * s) { Alignment = PenAlignment.Inset }) g.DrawRectangle(pen, r);
                Circulo(g, r, true);
            }
            else if (m == hover && !Bloqueada)
            {
                using (var br = new SolidBrush(Color.FromArgb(25, 0, 0, 0))) g.FillRectangle(br, r);
                Circulo(g, r, false);
            }
        }

        Size Medir(Graphics g, string texto)
        {
            var t = TextRenderer.MeasureText(g, texto, fPill, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(t.Width + (int)(12 * s), t.Height + (int)(5 * s));
        }

        void Pastilla(Graphics g, string texto, int x, int y, bool desdeAbajo, Color fondo)
        {
            var tam = Medir(g, texto);
            var r = new Rectangle(x, desdeAbajo ? y - tam.Height : y, tam.Width, tam.Height);
            using (var path = Redondo(r, tam.Height / 2f))
            using (var br = new SolidBrush(fondo)) g.FillPath(br, path);
            TextRenderer.DrawText(g, texto, fPill, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        void Circulo(Graphics g, Rectangle r, bool marcado)
        {
            float d = 24 * s;
            var c = new RectangleF(r.Right - d - 7 * s, r.Top + 7 * s, d, d);
            if (marcado)
            {
                using (var br = new SolidBrush(Acento)) g.FillEllipse(br, c);
                using (var pen = new Pen(Color.White, 2f * s)) g.DrawEllipse(pen, c);
                using (var pen = new Pen(Color.White, 2.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] {
                        new PointF(c.X + d * 0.28f, c.Y + d * 0.52f),
                        new PointF(c.X + d * 0.44f, c.Y + d * 0.68f),
                        new PointF(c.X + d * 0.74f, c.Y + d * 0.36f) });
            }
            else
            {
                using (var br = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(br, c);
                using (var pen = new Pen(Color.White, 2f * s)) g.DrawEllipse(pen, c);
            }
        }

        static GraphicsPath Redondo(RectangleF r, float radio)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------ mouse y teclado

        MediaItem Tocar(Point p, out MonthSection link)
        {
            link = null;
            int y = p.Y - AutoScrollPosition.Y;
            int paso = tile + gap;
            foreach (var sec in Secciones)
            {
                if (sec.View.Count == 0 || y < sec.Top || y >= sec.Top + sec.Height) continue;
                if (y < sec.Top + cab)
                {
                    if (sec.LinkRect.Contains(p.X, y)) link = sec;
                    return null;
                }
                int yy = y - sec.Top - cab, xx = p.X - margen;
                if (xx < 0 || yy % paso >= tile || xx % paso >= tile) return null;
                int c = xx / paso;
                if (c >= columnas) return null;
                int i = (yy / paso) * columnas + c;
                return i < sec.View.Count ? sec.View[i] : null;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            MonthSection link;
            var m = Tocar(e.Location, out link);
            if (m != hover || link != hoverLink) { hover = m; hoverLink = link; Invalidate(); }
            Cursor = (link != null || (m != null && !Bloqueada)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null || hoverLink != null) { hover = null; hoverLink = null; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left || Bloqueada) return;
            MonthSection link;
            var m = Tocar(e.Location, out link);
            if (link != null) { AlternarMes(link); return; }
            if (m == null) return;
            if ((ModifierKeys & Keys.Shift) != 0 && ancla != null && ancla != m) SeleccionarRango(ancla, m);
            else m.Selected = !m.Selected;
            ancla = m;
            Cambio();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            MonthSection link;
            var m = Tocar(e.Location, out link);
            if (m != null && Abrir != null) Abrir(m);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Desplazar(-e.Delta * (tile + gap) / 120);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
            if (Desplazado != null) Desplazado(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int pagina = Math.Max(tile, ClientSize.Height - tile);
            switch (e.KeyCode)
            {
                case Keys.Down: Desplazar(tile + gap); break;
                case Keys.Up: Desplazar(-(tile + gap)); break;
                case Keys.PageDown: Desplazar(pagina); break;
                case Keys.PageUp: Desplazar(-pagina); break;
                case Keys.Home: Desplazar(-AutoScrollMinSize.Height); break;
                case Keys.End: Desplazar(AutoScrollMinSize.Height); break;
            }
        }

        void Desplazar(int delta)
        {
            int y = -AutoScrollPosition.Y + delta;
            AutoScrollPosition = new Point(0, Math.Max(0, y));
            Invalidate();
            if (Desplazado != null) Desplazado(this, EventArgs.Empty);
        }

        void AlternarMes(MonthSection sec)
        {
            var nuevos = sec.View.Where(m => !m.Imported).ToList();
            if (nuevos.Count > 0 && nuevos.Any(m => !m.Selected)) foreach (var m in nuevos) m.Selected = true;
            else foreach (var m in sec.View) m.Selected = false;
            Cambio();
        }

        void SeleccionarRango(MediaItem a, MediaItem b)
        {
            var todos = Secciones.SelectMany(x => x.View).ToList();
            int i = todos.IndexOf(a), j = todos.IndexOf(b);
            if (i < 0 || j < 0) { b.Selected = !b.Selected; return; }
            for (int k = Math.Min(i, j); k <= Math.Max(i, j); k++)
                if (!todos[k].Imported || todos[k] == b) todos[k].Selected = true;
        }

        void Cambio()
        {
            Invalidate();
            if (SeleccionCambio != null) SeleccionCambio(this, EventArgs.Empty);
        }
    }
}
