using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ImportarIPhone
{
    // Busca el iPhone y lee la lista de fotos y videos, mes por mes (del más nuevo al más viejo).
    public class Lector
    {
        public Action<string> Estado;
        public Action<string> Conectado;
        public Action<int, int, int> Progreso;   // carpetas leídas, total, archivos encontrados
        public Action<MonthSection> MesListo;
        public Action<string> Error;
        public Action Terminado;
        public volatile bool Cancelar;

        static readonly HashSet<string> ExtFotos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".HEIC", ".HEIF", ".JPG", ".JPEG", ".PNG", ".GIF", ".DNG", ".TIF", ".TIFF", ".WEBP" };
        static readonly HashSet<string> ExtVideos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".MOV", ".MP4", ".M4V", ".AVI", ".3GP" };

        public static IShellItem BuscarTelefono(out string nombre)
        {
            nombre = null;
            IShellItem pc = Shell.FromParsingName(Shell.ThisPC);
            try
            {
                foreach (var c in Shell.Children(pc))
                {
                    string ruta = null;
                    try { ruta = Shell.Name(c, Shell.SIGDN_DESKTOPABSOLUTEPARSING); } catch { }
                    // vid_05ac = fabricante Apple
                    if (ruta != null && ruta.IndexOf("vid_05ac", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        nombre = Shell.Name(c, Shell.SIGDN_NORMALDISPLAY);
                        return c;
                    }
                    Marshal.ReleaseComObject(c);
                }
            }
            finally { Marshal.ReleaseComObject(pc); }
            return null;
        }

        public static bool TelefonoConectado()
        {
            try
            {
                string n;
                var t = BuscarTelefono(out n);
                if (t == null) return false;
                Marshal.ReleaseComObject(t);
                return true;
            }
            catch { return false; }
        }

        public void Leer()
        {
            try
            {
                IShellItem raiz = null;
                string nombre = null;
                string ultimoAviso = null;
                while (!Cancelar)
                {
                    var tel = BuscarTelefono(out nombre);
                    string aviso;
                    // Estado() recibe un código; la ventana lo muestra en el idioma elegido
                    if (tel == null) aviso = "conectar";
                    else
                    {
                        raiz = Shell.Children(tel).FirstOrDefault(Shell.IsFolder);
                        if (raiz != null && Shell.Children(raiz).Count > 0) break;
                        aviso = "desbloquear";
                    }
                    if (aviso != ultimoAviso) { Estado(aviso); ultimoAviso = aviso; }
                    Thread.Sleep(2000);
                }
                if (Cancelar) return;
                Conectado(nombre);

                var carpetas = Shell.Children(raiz).Where(Shell.IsFolder).ToList();
                // En algunos iPhone las carpetas de fotos están dentro de DCIM
                var dcim = carpetas.FirstOrDefault(c => string.Equals(Shell.Name(c, 0), "DCIM", StringComparison.OrdinalIgnoreCase));
                if (dcim != null) carpetas = Shell.Children(dcim).Where(Shell.IsFolder).ToList();
                var ordenadas = carpetas.Select(c => new { Item = c, Nombre = Shell.Name(c, 0) })
                                        .OrderByDescending(c => c.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

                MonthSection actual = null;
                int leidas = 0, encontrados = 0;
                Progreso(0, ordenadas.Count, 0);
                foreach (var c in ordenadas)
                {
                    if (Cancelar) return;
                    string mes = Formato.MesDeCarpeta(c.Nombre);
                    if (actual != null && actual.Key != mes) { Cerrar(actual); MesListo(actual); actual = null; }
                    if (actual == null) actual = new MonthSection { Key = mes };
                    encontrados += Recolectar(c.Item, mes, actual.Items);
                    leidas++;
                    Progreso(leidas, ordenadas.Count, encontrados);
                }
                if (actual != null) { Cerrar(actual); MesListo(actual); }
                Terminado();
            }
            catch (Exception ex)
            {
                if (!Cancelar) Error(ex.Message);
            }
        }

        int Recolectar(IShellItem carpeta, string mes, List<MediaItem> lista)
        {
            int n = 0;
            foreach (var it in Shell.Children(carpeta))
            {
                if (Cancelar) return n;
                if (Shell.IsFolder(it)) { n += Recolectar(it, mes, lista); continue; }
                string nombre = Shell.GetString(it, Shell.PKEY_FileName) ?? Shell.Name(it, 0);
                string ext = Path.GetExtension(nombre).ToUpperInvariant();
                bool foto = ExtFotos.Contains(ext), video = ExtVideos.Contains(ext);
                if (foto || video)   // los .AAE (ediciones de Apple) y otros archivos internos se ignoran
                {
                    lista.Add(new MediaItem
                    {
                        Name = nombre, Ext = ext, Size = Shell.GetSize(it), DateUtc = Shell.GetDate(it),
                        Idl = Shell.ToIdList(it), Month = mes, IsVideo = video
                    });
                    n++;
                }
                Marshal.ReleaseComObject(it);
            }
            return n;
        }

        // Junta cada Live Photo con su video corto y ordena del más nuevo al más viejo
        static void Cerrar(MonthSection s)
        {
            var fotos = new Dictionary<string, MediaItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in s.Items)
                if (m.Ext == ".HEIC" || m.Ext == ".JPG" || m.Ext == ".JPEG")
                    fotos[Path.GetFileNameWithoutExtension(m.Name)] = m;
            var quedan = new List<MediaItem>();
            foreach (var m in s.Items)
            {
                MediaItem foto;
                if (m.IsVideo && fotos.TryGetValue(Path.GetFileNameWithoutExtension(m.Name), out foto) && foto.Companion == null)
                {
                    foto.Companion = m;
                    continue;
                }
                quedan.Add(m);
            }
            s.Items = quedan.OrderByDescending(m => m.DateUtc ?? DateTime.MinValue)
                            .ThenByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    // Carga las miniaturas en segundo plano, primero las que están en pantalla.
    //
    // Windows entrega dos tipos de miniatura para el iPhone, y alterna entre ellas:
    //  - la que trae el iPhone ("llena"): rápida, pero en fotos HEIC/JPG originales puede venir sin girar;
    //  - la que genera Windows (con bordes transparentes): siempre bien girada.
    // Si recibimos una "llena" horizontal de una foto, la mostramos y volvemos a pedirla unos segundos
    // después, para cambiarla por la buena cuando Windows la tenga.
    public class Miniaturas
    {
        class Reintento { public MediaItem Item; public int Veces; public DateTime Cuando; public bool Mostrada; }

        readonly Control ui;
        readonly int px;
        readonly object candado = new object();
        List<MediaItem> cola = new List<MediaItem>();
        HashSet<int> visibles = new HashSet<int>();
        readonly HashSet<int> pedidas = new HashSet<int>();
        readonly Dictionary<int, Reintento> provisionales = new Dictionary<int, Reintento>();
        readonly AutoResetEvent senal = new AutoResetEvent(false);
        public Action<MediaItem, Bitmap> Lista;   // se llama en el hilo de la ventana
        public Action<bool> AvisoBloqueo;         // true: el iPhone no entrega miniaturas (casi siempre: está bloqueado)
        int fallosSeguidos;

        public Miniaturas(Control ui, int px)
        {
            this.ui = ui; this.px = px;
            var t = new Thread(Bucle) { IsBackground = true, Name = "Miniaturas" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // Recibe lo que está en pantalla
        public void Querer(List<MediaItem> enPantalla)
        {
            bool hay;
            lock (candado)
            {
                visibles = new HashSet<int>(enPantalla.Select(i => i.Id));
                cola = enPantalla.Where(i => !pedidas.Contains(i.Id)).ToList();
                hay = cola.Count > 0;
            }
            if (hay) senal.Set();
        }

        public void Olvidar(int id) { lock (candado) { pedidas.Remove(id); provisionales.Remove(id); } }
        public void Reiniciar() { lock (candado) { cola.Clear(); pedidas.Clear(); provisionales.Clear(); visibles.Clear(); } }

        void Bucle()
        {
            while (true)
            {
                senal.WaitOne(1000);
                while (true)
                {
                    MediaItem m;
                    Reintento r = null;
                    lock (candado)
                    {
                        if (cola.Count > 0)
                        {
                            m = cola[0];
                            cola.RemoveAt(0);
                            if (!pedidas.Add(m.Id)) continue;
                            provisionales.TryGetValue(m.Id, out r);
                        }
                        else
                        {
                            var ahora = DateTime.UtcNow;
                            r = provisionales.Values.FirstOrDefault(x => x.Cuando <= ahora && visibles.Contains(x.Item.Id));
                            if (r == null) break;
                            m = r.Item;
                            r.Veces++;
                            r.Cuando = ahora.AddSeconds(r.Mostrada ? 3 : 4);
                            // Las que ya se ven se reintentan pocas veces; las que fallaron, hasta que el iPhone responda
                            if (r.Mostrada && r.Veces >= 5) provisionales.Remove(m.Id);
                        }
                    }

                    Bitmap b = null;
                    bool provisional = false;
                    try { b = Crear(m, out provisional); } catch { b = null; }

                    bool mostrar;
                    lock (candado)
                    {
                        if (r == null) r = new Reintento { Item = m };
                        if (b != null && !provisional) { provisionales.Remove(m.Id); mostrar = true; }
                        else if (b != null)
                        {
                            mostrar = !r.Mostrada;
                            r.Mostrada = true;
                            if (!provisionales.ContainsKey(m.Id) && r.Veces < 5) { r.Cuando = DateTime.UtcNow.AddSeconds(2); provisionales[m.Id] = r; }
                        }
                        else
                        {
                            mostrar = false;
                            if (!provisionales.ContainsKey(m.Id) && !r.Mostrada) { r.Cuando = DateTime.UtcNow.AddSeconds(4); provisionales[m.Id] = r; }
                        }
                    }

                    if (b == null) { if (++fallosSeguidos == 6) Avisar(true); }
                    else { if (fallosSeguidos >= 6) Avisar(false); fallosSeguidos = 0; }

                    if (!mostrar) { if (b != null) b.Dispose(); continue; }
                    var mm = m; var bb = b;
                    try { ui.BeginInvoke(new Action(() => Lista(mm, bb))); }
                    catch { if (bb != null) bb.Dispose(); }
                }
            }
        }

        void Avisar(bool bloqueado)
        {
            try { if (AvisoBloqueo != null) ui.BeginInvoke(new Action(() => AvisoBloqueo(bloqueado))); } catch { }
        }

        Bitmap Crear(MediaItem m, out bool provisional)
        {
            provisional = false;
            if (Demo.Activo) return Demo.Imagen(m, px);
            // En una Live Photo usamos la miniatura del video: el iPhone la entrega ya girada correctamente
            var fuente = m.Companion ?? m;
            bool llena;
            Bitmap b = Leer(fuente, out llena);
            if (b == null && m.Companion != null) { fuente = m; b = Leer(m, out llena); }
            if (b == null) return null;
            using (b)
            {
                provisional = fuente == m && llena && b.Width > b.Height &&
                              (m.Ext == ".HEIC" || m.Ext == ".HEIF" || m.Ext == ".JPG" || m.Ext == ".JPEG");
                return Cuadrado(b, px);
            }
        }

        Bitmap Leer(MediaItem m, out bool llena)
        {
            llena = true;
            IShellItem it = Shell.FromIdList(m.Idl);
            try
            {
                Bitmap b = Shell.Thumbnail(it, Math.Max(px, 160), Shell.SIIGBF_THUMBNAILONLY);
                if (b == null) return null;
                Rectangle vis = Visible(b);
                if (vis.Width >= b.Width - 2 && vis.Height >= b.Height - 2) return b;
                // Tiene bordes transparentes: los quitamos
                llena = false;
                Bitmap recorte = b.Clone(vis, b.PixelFormat);
                b.Dispose();
                return recorte;
            }
            finally { Marshal.ReleaseComObject(it); }
        }

        // Zona de la imagen que no es transparente
        static Rectangle Visible(Bitmap b)
        {
            var todo = new Rectangle(0, 0, b.Width, b.Height);
            if (!Image.IsAlphaPixelFormat(b.PixelFormat)) return todo;
            BitmapData d = b.LockBits(todo, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int fila = Math.Abs(d.Stride);
                var buf = new byte[fila * b.Height];
                for (int y = 0; y < b.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), buf, y * fila, fila);
                int minX = b.Width, minY = b.Height, maxX = -1, maxY = -1;
                for (int y = 0; y < b.Height; y++)
                    for (int x = 0; x < b.Width; x++)
                        if (buf[y * fila + x * 4 + 3] > 16)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                if (maxX < 0) return todo;
                return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
            finally { b.UnlockBits(d); }
        }

        static Bitmap Cuadrado(Bitmap src, int px)
        {
            var dst = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dst))
            using (var attr = new ImageAttributes())
            {
                attr.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int lado = Math.Min(src.Width, src.Height);
                g.DrawImage(src, new Rectangle(0, 0, px, px), (src.Width - lado) / 2, (src.Height - lado) / 2, lado, lado, GraphicsUnit.Pixel, attr);
            }
            return dst;
        }
    }
}
