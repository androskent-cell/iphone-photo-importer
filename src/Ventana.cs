using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ImportarIPhone
{
    public class Ventana : Form
    {
        static readonly Color FondoLateral = Color.FromArgb(245, 245, 247);
        static readonly Color Borde = Color.FromArgb(222, 222, 228);
        static readonly Color GrisBoton = Color.FromArgb(200, 200, 206);

        public static string NombreApp { get { return L.T("Importar fotos del iPhone", "iPhone Photo Importer"); } }

        readonly Biblioteca biblioteca = new Biblioteca();
        readonly float s;
        readonly bool destinoTemporal;   // --destino desde la línea de comandos (pruebas): no se guarda

        Panel cabecera, pie, lateral;
        Cuadricula grid;
        ListBox meses;
        Label lblTitulo, lblEstado, lblMostrar, lblSel, lblProgreso, lblDestino;
        LinkLabel lnkTodo, lnkQuitar, lnkCambiar, lnkAbrirCarpeta, lnkIdioma;
        ComboBox cmbMostrar;
        CheckBox chkOcultar, chkLive;
        Button btnActualizar, btnCopiar;
        ProgressBar barra;

        Miniaturas miniaturas;
        Lector lector;
        int generacion;
        string telefono = "iPhone";
        // buscando | conectar | desbloquear | leyendo | error | listo
        string fase = "buscando";
        bool leyendo { get { return fase == "buscando" || fase == "conectar" || fase == "desbloquear" || fase == "leyendo"; } }
        int carpetasLeidas, carpetasTotal, archivosVistos;
        volatile bool copiando, detener;
        bool sincronizando;
        bool iPhoneBloqueado;
        readonly Dictionary<int, Bitmap> cache = new Dictionary<int, Bitmap>();
        readonly LinkedList<int> ordenCache = new LinkedList<int>();
        const int MaxCache = 1500;

        // Para pruebas automáticas
        public Action<Ventana> AlCargarMes;
        public Action<Ventana, string> AlTerminarCopia;

        public Ventana(string destinoForzado)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.75f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            var area = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min((int)(1320 * s), area.Width - 40), Math.Min((int)(860 * s), area.Height - 40));
            MinimumSize = new Size((int)(1000 * s), (int)(600 * s));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            destinoTemporal = destinoForzado != null;
            // En modo demo se muestra una carpeta de ejemplo, no la real del usuario
            biblioteca.Abrir(Demo.Activo ? @"C:\Users\Me\Pictures\iPhone" : destinoForzado ?? Config.LeerDestino());

            ConstruirCabecera();
            ConstruirPie();
            ConstruirLateral();
            ConstruirCuadricula();
            ConstruirAviso();
            // Orden de acoplamiento: cabecera arriba, debajo el aviso, luego pie, lista de meses y cuadrícula
            Controls.Add(grid);
            Controls.Add(lateral);
            Controls.Add(pie);
            Controls.Add(aviso);
            Controls.Add(cabecera);

            miniaturas = new Miniaturas(this, grid.TamanoMiniatura);
            miniaturas.Lista = MiniaturaLista;
            miniaturas.AvisoBloqueo = b => { iPhoneBloqueado = b; ActualizarEstado(); };
            AplicarTextos();
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // ------------------------------------------------------------ idioma

        // Pone todos los textos en el idioma actual (al abrir y al cambiar de idioma)
        void AplicarTextos()
        {
            Text = NombreApp;
            lblTitulo.Text = L.T("Fotos del iPhone", "iPhone Photos");
            lnkIdioma.Text = L.En ? "Español" : "English";
            lblMostrar.Text = L.T("Mostrar:", "Show:");
            int modo = Math.Max(0, cmbMostrar.SelectedIndex);
            cmbMostrar.Items.Clear();
            cmbMostrar.Items.AddRange(new object[] { L.T("Todo", "Everything"), L.T("Solo fotos", "Photos only"), L.T("Solo videos", "Videos only") });
            cmbMostrar.SelectedIndex = modo;
            chkOcultar.Text = L.T("Ocultar lo que ya está en la PC", "Hide what's already on this PC");
            btnActualizar.Text = L.T("Volver a leer", "Reload");
            lnkTodo.Text = L.T("Seleccionar todo lo nuevo", "Select all new");
            lnkQuitar.Text = L.T("Quitar selección", "Clear selection");
            lnkCambiar.Text = L.T("Cambiar carpeta", "Change folder");
            lnkAbrirCarpeta.Text = L.T("Abrir carpeta", "Open folder");
            chkLive.Text = L.T("Incluir el video corto de las Live Photos", "Include the short video of Live Photos");
            lnkAviso.Text = L.T("Cómo arreglarlo", "How to fix it");
            ActualizarDestino();
            ActualizarEstado();
            ActualizarSeleccion();
            RevisarAviso();
            AcomodarCabecera();
            AcomodarPie();
            meses.Invalidate();
            grid.Invalidate();
        }

        void CambiarIdioma()
        {
            if (copiando) return;
            L.Cambiar(!L.En);
            AplicarTextos();
        }

        // ------------------------------------------------------------ construcción

        void ConstruirCabecera()
        {
            cabecera = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = P(70), BackColor = Color.White };
            cabecera.Paint += (o, e) => { using (var pen = new Pen(Borde)) e.Graphics.DrawLine(pen, 0, cabecera.Height - 1, cabecera.Width, cabecera.Height - 1); };
            lblTitulo = new Label { Font = new Font("Segoe UI Semibold", 16f), ForeColor = Cuadricula.ColorTexto, AutoSize = true };
            lblEstado = new Label { ForeColor = Cuadricula.ColorSecundario, AutoSize = false, AutoEllipsis = true };
            lnkIdioma = Enlace("", (o, e) => CambiarIdioma());
            lblMostrar = new Label { ForeColor = Cuadricula.ColorSecundario, AutoSize = true };
            cmbMostrar = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = P(130) };
            cmbMostrar.SelectedIndexChanged += (o, e) => AplicarFiltro();
            chkOcultar = new CheckBox { AutoSize = true };
            chkOcultar.CheckedChanged += (o, e) => AplicarFiltro();
            btnActualizar = new Button { Size = new Size(P(120), P(32)) };
            btnActualizar.Click += (o, e) => { if (!copiando) IniciarLectura(); };
            cabecera.Controls.AddRange(new Control[] { lblTitulo, lblEstado, lnkIdioma, lblMostrar, cmbMostrar, chkOcultar, btnActualizar });
            cabecera.Resize += (o, e) => AcomodarCabecera();
        }

        void AcomodarCabecera()
        {
            int H = cabecera.Height, m = P(20), x = cabecera.Width - m;
            btnActualizar.Location = new Point(x -= btnActualizar.Width, (H - btnActualizar.Height) / 2);
            x -= P(18);
            chkOcultar.Location = new Point(x -= chkOcultar.PreferredSize.Width, (H - chkOcultar.PreferredSize.Height) / 2);
            x -= P(18);
            cmbMostrar.Location = new Point(x -= cmbMostrar.Width, (H - cmbMostrar.Height) / 2);
            x -= P(6);
            lblMostrar.Location = new Point(x -= lblMostrar.PreferredWidth, (H - lblMostrar.PreferredHeight) / 2);
            x -= P(26);
            lnkIdioma.Location = new Point(x -= lnkIdioma.PreferredWidth, (H - lnkIdioma.PreferredHeight) / 2);
            lblTitulo.Location = new Point(m - P(2), P(8));
            lblEstado.Location = new Point(m, lblTitulo.Bottom + P(1));
            lblEstado.Size = new Size(Math.Max(P(100), lnkIdioma.Left - m - P(24)), P(20));
        }

        void ConstruirPie()
        {
            pie = new DoubleBufferedPanel { Dock = DockStyle.Bottom, Height = P(84), BackColor = FondoLateral };
            pie.Paint += (o, e) => { using (var pen = new Pen(Borde)) e.Graphics.DrawLine(pen, 0, 0, pie.Width, 0); };
            lblSel = new Label { Font = new Font("Segoe UI Semibold", 11.5f), ForeColor = Cuadricula.ColorTexto, AutoSize = true };
            lnkTodo = Enlace("", (o, e) => SeleccionarTodoLoNuevo());
            lnkQuitar = Enlace("", (o, e) => QuitarSeleccion());
            barra = new ProgressBar { Maximum = 1000, Visible = false, Height = P(8) };
            lblProgreso = new Label { ForeColor = Cuadricula.ColorSecundario, AutoSize = false, AutoEllipsis = true, Visible = false };
            lblDestino = new Label { ForeColor = Cuadricula.ColorTexto, AutoSize = false, AutoEllipsis = true };
            lnkCambiar = Enlace("", (o, e) => CambiarDestino());
            lnkAbrirCarpeta = Enlace("", (o, e) => AbrirCarpeta());
            chkLive = new CheckBox { AutoSize = true, Checked = Config.LeerLive() };
            chkLive.CheckedChanged += (o, e) => { Config.GuardarLive(chkLive.Checked); ActualizarSeleccion(); };
            btnCopiar = new Button
            {
                FlatStyle = FlatStyle.Flat, BackColor = Cuadricula.Acento, ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f), Size = new Size(P(210), P(46)), Cursor = Cursors.Hand
            };
            btnCopiar.FlatAppearance.BorderSize = 0;
            btnCopiar.Click += (o, e) => Copiar();
            pie.Controls.AddRange(new Control[] { lblSel, lnkTodo, lnkQuitar, barra, lblProgreso, lblDestino, lnkCambiar, lnkAbrirCarpeta, chkLive, btnCopiar });
            pie.Resize += (o, e) => AcomodarPie();
        }

        LinkLabel Enlace(string texto, LinkLabelLinkClickedEventHandler accion)
        {
            var l = new LinkLabel
            {
                Text = texto, AutoSize = true, LinkColor = Cuadricula.Acento, ActiveLinkColor = Cuadricula.Acento,
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Color.Transparent
            };
            l.LinkClicked += accion;
            return l;
        }

        void AcomodarPie()
        {
            int W = pie.Width, H = pie.Height, m = P(20);
            btnCopiar.Location = new Point(W - m - btnCopiar.Width, (H - btnCopiar.Height) / 2);
            int anchoDestino = P(340);
            int xd = btnCopiar.Left - P(24) - anchoDestino;
            lblDestino.Location = new Point(xd, P(12));
            lblDestino.Size = new Size(anchoDestino, P(20));
            lnkCambiar.Location = new Point(xd - P(1), P(34));
            lnkAbrirCarpeta.Location = new Point(lnkCambiar.Right + P(14), P(34));
            chkLive.Location = new Point(xd, P(56));

            lblSel.Location = new Point(m, P(14));
            lnkTodo.Location = new Point(m, P(48));
            lnkQuitar.Location = new Point(lnkTodo.Right + P(16), P(48));

            int x1 = m + P(330), x2 = xd - P(30);
            barra.Location = new Point(x1, P(24));
            barra.Width = Math.Max(P(60), x2 - x1);
            lblProgreso.Location = new Point(x1, P(40));
            lblProgreso.Size = new Size(Math.Max(P(60), x2 - x1), P(38));
        }

        void ConstruirLateral()
        {
            lateral = new DoubleBufferedPanel { Dock = DockStyle.Left, Width = P(210), BackColor = FondoLateral, Padding = new Padding(P(10), P(12), P(10), P(10)) };
            lateral.Paint += (o, e) => { using (var pen = new Pen(Borde)) e.Graphics.DrawLine(pen, lateral.Width - 1, 0, lateral.Width - 1, lateral.Height); };
            meses = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = FondoLateral,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = P(34), IntegralHeight = false
            };
            meses.DrawItem += DibujarMes;
            meses.SelectedIndexChanged += (o, e) =>
            {
                if (sincronizando) return;
                var sec = meses.SelectedItem as MonthSection;
                if (sec != null) grid.IrA(sec);
            };
            lateral.Controls.Add(meses);
        }

        void DibujarMes(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var sec = (MonthSection)meses.Items[e.Index];
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(FondoLateral)) g.FillRectangle(br, e.Bounds);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var r = e.Bounds; r.Inflate(-P(2), -P(2));
            if (sel)
                using (var br = new SolidBrush(Color.FromArgb(222, 234, 255)))
                using (var path = new GraphicsPath())
                {
                    int d = P(12);
                    path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    g.FillPath(br, path);
                }
            int nuevos = sec.View.Count(x => !x.Imported);
            string derecha = nuevos > 0 ? Formato.Numero(nuevos) : "✓";
            var colorDer = nuevos > 0 ? Cuadricula.Acento : Color.FromArgb(36, 160, 70);
            var rt = new Rectangle(r.X + P(10), r.Y, r.Width - P(20), r.Height);
            TextRenderer.DrawText(g, Formato.NombreMesCorto(sec.Key), e.Font, rt, Cuadricula.ColorTexto, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(g, derecha, e.Font, rt, colorDer, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        void ConstruirCuadricula()
        {
            grid = new Cuadricula { Dock = DockStyle.Fill };
            grid.Escala(s);
            grid.Miniatura = m => { Bitmap b; return cache.TryGetValue(m.Id, out b) ? b : null; };
            grid.PedirMiniaturas = lista => miniaturas.Querer(lista);
            grid.SeleccionCambio += (o, e) => { ActualizarSeleccion(); meses.Invalidate(); };
            grid.Abrir += VerEnGrande;
            grid.Desplazado += (o, e) =>
            {
                var sec = grid.SeccionArriba();
                if (sec == null || meses.SelectedItem == sec) return;
                sincronizando = true;
                meses.SelectedItem = sec;
                sincronizando = false;
            };
        }

        // ------------------------------------------------------------ lectura del iPhone

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AcomodarCabecera();
            AcomodarPie();
            LimpiarVistaPrevia();
            IniciarLectura();
        }

        void EnUI(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void IniciarLectura()
        {
            if (lector != null) lector.Cancelar = true;
            int gen = ++generacion;
            grid.Secciones.Clear();
            meses.Items.Clear();
            foreach (var b in cache.Values) b.Dispose();
            cache.Clear(); ordenCache.Clear();
            miniaturas.Reiniciar();
            fase = "buscando"; carpetasLeidas = carpetasTotal = archivosVistos = 0;
            aviso.Visible = false;
            grid.AutoScrollPosition = Point.Empty;
            grid.Reacomodar();
            btnActualizar.Enabled = false;
            ActualizarEstado();
            ActualizarSeleccion();

            if (Demo.Activo)
            {
                telefono = "iPhone";
                foreach (var sec in Demo.Secciones()) AgregarMes(sec);
                fase = "listo"; btnActualizar.Enabled = true;
                ActualizarEstado();
                return;
            }

            var l = new Lector();
            l.Estado = codigo => EnUI(() => { if (gen == generacion) { fase = codigo; ActualizarEstado(); } });
            l.Conectado = nombre => EnUI(() => { if (gen == generacion) { telefono = nombre; fase = "leyendo"; ActualizarEstado(); } });
            l.Progreso = (leidas, total, encontrados) => EnUI(() =>
            {
                if (gen != generacion) return;
                carpetasLeidas = leidas; carpetasTotal = total; archivosVistos = encontrados;
                ActualizarEstado();
            });
            l.MesListo = sec => EnUI(() => { if (gen == generacion) AgregarMes(sec); });
            l.Error = msg => EnUI(() =>
            {
                if (gen != generacion) return;
                fase = "error"; btnActualizar.Enabled = !copiando;
                ActualizarEstado();
            });
            l.Terminado = () => EnUI(() =>
            {
                if (gen != generacion) return;
                fase = "listo"; btnActualizar.Enabled = !copiando;
                ActualizarEstado();
                RevisarAviso();
            });
            lector = l;
            var t = new Thread(l.Leer) { IsBackground = true, Name = "Lector" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        void AgregarMes(MonthSection sec)
        {
            if (!Demo.Activo) foreach (var m in sec.Items) m.Imported = biblioteca.YaImportado(m);
            grid.Secciones.Add(sec);
            Filtrar(sec);
            if (sec.View.Count > 0) meses.Items.Add(sec);
            if (meses.SelectedIndex < 0 && meses.Items.Count > 0) { sincronizando = true; meses.SelectedIndex = 0; sincronizando = false; }
            grid.Reacomodar();
            ActualizarEstado();
            ActualizarSeleccion();
            RevisarAviso();
            if (AlCargarMes != null) AlCargarMes(this);
        }

        // Texto de estado (debajo del título) y mensaje del centro cuando todavía no hay fotos
        void ActualizarEstado()
        {
            var todos = grid.Secciones.SelectMany(x => x.Items).ToList();
            string estado, vacio = "";
            switch (fase)
            {
                case "conectar":
                    estado = vacio = L.T("Conecta tu iPhone con el cable USB…", "Connect your iPhone with a USB cable…");
                    break;
                case "desbloquear":
                    estado = vacio = L.T("Desbloquea tu iPhone y, si te lo pregunta, toca «Confiar».",
                                         "Unlock your iPhone and, if it asks, tap “Trust”.");
                    break;
                case "error":
                    estado = vacio = L.T("Se perdió la conexión con el iPhone. Revisa el cable, desbloquéalo y toca «Volver a leer».",
                                         "Lost the connection to the iPhone. Check the cable, unlock it and click “Reload”.");
                    break;
                case "leyendo":
                    vacio = L.T("Leyendo ", "Reading ") + telefono + "…\n\n" +
                            L.T("La primera vez después de conectarlo puede tardar unos minutos.",
                                "The first time after plugging it in, this can take a few minutes.");
                    estado = carpetasTotal == 0 ? L.T("Leyendo ", "Reading ") + telefono + "…"
                           : L.T("Leyendo ", "Reading ") + telefono + "…  " + carpetasLeidas + L.T(" de ", " of ") + carpetasTotal +
                             L.T(" carpetas  ·  ", " folders  ·  ") + Formato.Numero(archivosVistos) +
                             L.T(" archivos encontrados. Ya puedes ir eligiendo.", " files found. You can start choosing.");
                    break;
                case "listo":
                    if (todos.Count == 0)
                        estado = vacio = L.T("El iPhone no muestra fotos ni videos. Desbloquéalo y toca «Volver a leer».",
                                             "The iPhone isn't showing any photos or videos. Unlock it and click “Reload”.");
                    else
                    {
                        int nuevos = todos.Count(m => !m.Imported);
                        long bytes = todos.Sum(m => m.Size + (m.Companion != null ? m.Companion.Size : 0));
                        estado = telefono + "  ·  " + Formato.Numero(todos.Count) + L.T(" fotos y videos (", " photos and videos (") +
                                 Formato.Tamano(bytes) + ")  ·  " +
                                 (nuevos > 0 ? Formato.Numero(nuevos) + L.T(" todavía no están en la PC", " not on this PC yet")
                                             : L.T("todo está en la PC", "everything is on this PC"));
                    }
                    break;
                default:
                    estado = vacio = L.T("Buscando el iPhone…", "Looking for the iPhone…");
                    break;
            }
            if (iPhoneBloqueado)
                estado = L.T("⚠  Tu iPhone está bloqueado: desbloquéalo para ver las fotos y poder copiarlas.",
                             "⚠  Your iPhone is locked: unlock it to see your photos and copy them.");
            lblEstado.ForeColor = iPhoneBloqueado ? Color.FromArgb(196, 110, 0) : Cuadricula.ColorSecundario;
            lblEstado.Text = estado;
            if (grid.TextoVacio != vacio) { grid.TextoVacio = vacio; grid.Invalidate(); }
        }

        // ------------------------------------------------------------ aviso: "¿faltan fotos más nuevas?"
        //
        // A veces el iPhone se queda con una lista vieja de lo que comparte por el cable y no muestra
        // las fotos más recientes. Desde la PC no se puede arreglar, pero sí detectar: si lo más nuevo
        // que comparte tiene más de 45 días, avisamos y explicamos cómo arreglarlo en el iPhone.

        public static DateTime? HoyDePrueba;   // --hoy AAAA-MM-DD, solo para probar el aviso
        static DateTime Ahora { get { return HoyDePrueba ?? DateTime.UtcNow; } }

        Panel aviso;
        Label lblAviso, btnCerrarAviso;
        LinkLabel lnkAviso;
        DateTime masReciente;

        void ConstruirAviso()
        {
            var fondo = Color.FromArgb(255, 245, 222);
            aviso = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = P(46), BackColor = fondo, Visible = false };
            aviso.Paint += (o, e) => { using (var pen = new Pen(Color.FromArgb(240, 205, 140))) e.Graphics.DrawLine(pen, 0, aviso.Height - 1, aviso.Width, aviso.Height - 1); };
            lblAviso = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 70, 0), BackColor = fondo };
            lnkAviso = Enlace("", (o, e) => MostrarAyudaAviso());
            lnkAviso.Font = new Font("Segoe UI Semibold", 9.75f);
            btnCerrarAviso = new Label { Text = "✕", AutoSize = true, ForeColor = Color.FromArgb(150, 110, 40), BackColor = fondo, Cursor = Cursors.Hand };
            btnCerrarAviso.Click += (o, e) =>
            {
                // No volver a mostrarlo mientras lo más reciente siga siendo el mismo mes
                Config.GuardarAvisoOculto(masReciente.ToLocalTime().ToString("yyyy-MM"));
                aviso.Visible = false;
            };
            aviso.Controls.AddRange(new Control[] { lblAviso, lnkAviso, btnCerrarAviso });
            aviso.Resize += (o, e) => AcomodarAviso();
        }

        void AcomodarAviso()
        {
            int H = aviso.Height, m = P(20);
            lblAviso.Location = new Point(m, (H - lblAviso.PreferredHeight) / 2);
            lnkAviso.Location = new Point(lblAviso.Left + lblAviso.PreferredWidth + P(10), (H - lnkAviso.PreferredHeight) / 2);
            btnCerrarAviso.Location = new Point(aviso.Width - m - btnCerrarAviso.PreferredWidth, (H - btnCerrarAviso.PreferredHeight) / 2);
        }

        void RevisarAviso()
        {
            var fechas = grid.Secciones.SelectMany(x => x.Items).Where(m => m.DateUtc.HasValue).Select(m => m.DateUtc.Value).ToList();
            if (fechas.Count == 0) { aviso.Visible = false; return; }
            masReciente = fechas.Max();
            string mes = masReciente.ToLocalTime().ToString("yyyy-MM");
            bool viejo = (Ahora - masReciente).TotalDays > 45;
            if (!viejo || Config.LeerAvisoOculto() == mes) { aviso.Visible = false; return; }
            lblAviso.Text = "⚠   " + L.T("Lo más reciente que comparte tu iPhone es del ", "The newest item your iPhone is sharing is from ") +
                            Formato.FechaLarga(masReciente.ToLocalTime()) +
                            L.T(".  ¿Faltan fotos o videos más nuevos?", ".  Missing newer photos or videos?");
            aviso.Visible = true;
            AcomodarAviso();
        }

        void MostrarAyudaAviso()
        {
            string fecha = Formato.FechaLarga(masReciente.ToLocalTime());
            string texto = L.En
                ? "The newest item your iPhone shares over the cable is from " + fecha + ".\n\n" +
                  "If the Photos app on your iPhone has newer photos or videos, the iPhone is stuck with an old list " +
                  "of what it shares with the PC. This has to be fixed on the iPhone; this app can't do it from the PC.\n\n" +
                  "1.  Restart the iPhone (turn it off and on). Connect it, unlock it and click “Reload”.\n\n" +
                  "2.  If they still don't show up, on the iPhone go to:\n" +
                  "      Settings > General > Transfer or Reset iPhone > Reset >\n" +
                  "      Reset Location & Privacy\n\n" +
                  "      This doesn't delete photos or data. Apps will ask for permissions again (location, camera…) " +
                  "and you'll have to tap “Trust” again when you connect the iPhone. Then click “Reload”.\n\n" +
                  "If you use iCloud Photos with “Optimize iPhone Storage”, photos that are only in iCloud can't come " +
                  "over the cable either: download them from icloud.com/photos or with iCloud for Windows."
                : "Lo más reciente que tu iPhone comparte por el cable es del " + fecha + ".\n\n" +
                  "Si en la app Fotos del iPhone tienes fotos o videos más nuevos, el iPhone se quedó con una lista vieja " +
                  "de lo que comparte con la PC. Esto se arregla en el iPhone; esta app no puede hacerlo desde la PC.\n\n" +
                  "1.  Reinicia el iPhone (apágalo y préndelo). Conéctalo, desbloquéalo y toca «Volver a leer».\n\n" +
                  "2.  Si siguen sin aparecer, en el iPhone ve a:\n" +
                  "      Ajustes > General > Transferir o restablecer iPhone > Restablecer >\n" +
                  "      Restablecer localización y privacidad\n" +
                  "      (en inglés: Reset Location & Privacy)\n\n" +
                  "      No borra fotos ni datos. Las apps te volverán a pedir permisos (ubicación, cámara…) " +
                  "y al conectar el iPhone tendrás que tocar «Confiar» otra vez. Después toca «Volver a leer».\n\n" +
                  "Si usas Fotos de iCloud con «Optimizar almacenamiento», las fotos que solo están en iCloud tampoco " +
                  "salen por el cable: descárgalas desde icloud.com/photos o con iCloud para Windows.";
            MessageBox.Show(this, texto, L.T("¿Faltan fotos o videos más nuevos?", "Missing newer photos or videos?"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------ filtros y selección

        bool Pasa(MediaItem m)
        {
            int modo = cmbMostrar.SelectedIndex;
            if (modo == 1 && m.IsVideo) return false;
            if (modo == 2 && !m.IsVideo) return false;
            if (chkOcultar.Checked && m.Imported && !m.Selected) return false;
            return true;
        }

        void Filtrar(MonthSection sec) { sec.View = sec.Items.Where(Pasa).ToList(); }

        void AplicarFiltro()
        {
            var actual = meses.SelectedItem as MonthSection;
            foreach (var sec in grid.Secciones) Filtrar(sec);
            sincronizando = true;
            meses.BeginUpdate();
            meses.Items.Clear();
            foreach (var sec in grid.Secciones) if (sec.View.Count > 0) meses.Items.Add(sec);
            meses.EndUpdate();
            if (actual != null && meses.Items.Contains(actual)) meses.SelectedItem = actual;
            else if (meses.Items.Count > 0) meses.SelectedIndex = 0;
            sincronizando = false;
            grid.Reacomodar();
            if (actual != null && actual.View.Count > 0) grid.IrA(actual);
        }

        List<MediaItem> Seleccionadas() { return grid.Secciones.SelectMany(x => x.Items).Where(m => m.Selected).ToList(); }

        long Bytes(IEnumerable<MediaItem> items)
        {
            return items.Sum(m => m.Size + (chkLive.Checked && m.Companion != null ? m.Companion.Size : 0));
        }

        void ActualizarSeleccion()
        {
            var sel = Seleccionadas();
            int n = sel.Count;
            lblSel.Text = n == 0 ? L.T("Toca las fotos y videos que quieras pasar", "Click the photos and videos you want to copy")
                                 : Formato.Numero(n) + (n == 1 ? L.T(" seleccionada", " selected") : L.T(" seleccionadas", " selected")) +
                                   "  ·  " + Formato.Tamano(Bytes(sel));
            lnkQuitar.Visible = n > 0 && !copiando;
            lnkTodo.Visible = !copiando && grid.Secciones.Any(x => x.View.Any(m => !m.Imported && !m.Selected));
            lnkIdioma.Enabled = !copiando;
            if (copiando)
            {
                btnCopiar.Text = detener ? L.T("Deteniendo…", "Stopping…") : L.T("Detener", "Stop");
                btnCopiar.Enabled = !detener;
                btnCopiar.BackColor = Color.FromArgb(90, 90, 96);
            }
            else
            {
                btnCopiar.Text = n > 0 ? L.T("Copiar ", "Copy ") + Formato.Numero(n) + L.T(" a la PC", " to PC") : L.T("Copiar a la PC", "Copy to PC");
                btnCopiar.Enabled = n > 0;
                btnCopiar.BackColor = n > 0 ? Cuadricula.Acento : GrisBoton;
            }
            if (pie.Width > 0) AcomodarPie();
        }

        public void SeleccionarTodoLoNuevo()
        {
            foreach (var sec in grid.Secciones) foreach (var m in sec.View) if (!m.Imported) m.Selected = true;
            grid.Invalidate(); meses.Invalidate();
            ActualizarSeleccion();
        }

        void QuitarSeleccion()
        {
            foreach (var sec in grid.Secciones) foreach (var m in sec.Items) m.Selected = false;
            grid.Invalidate(); meses.Invalidate();
            ActualizarSeleccion();
        }

        // Para pruebas: selecciona los primeros N nuevos que estén en pantalla
        public void SeleccionarPrimeros(int n)
        {
            foreach (var m in grid.Secciones.SelectMany(x => x.View).Where(x => !x.Imported).Take(n)) m.Selected = true;
            grid.Invalidate();
            ActualizarSeleccion();
        }

        public void CopiarAhora() { Copiar(); }
        public int MesesCargados { get { return grid.Secciones.Count; } }
        public bool Leyendo { get { return leyendo; } }

        // ------------------------------------------------------------ miniaturas

        void MiniaturaLista(MediaItem m, Bitmap b)
        {
            if (b == null) return;   // sin miniatura: se queda el cuadro gris con la extensión
            Bitmap viejo;
            if (cache.TryGetValue(m.Id, out viejo)) { viejo.Dispose(); }
            else ordenCache.AddLast(m.Id);
            cache[m.Id] = b;
            while (cache.Count > MaxCache)
            {
                int id = ordenCache.First.Value;
                ordenCache.RemoveFirst();
                Bitmap v;
                if (cache.TryGetValue(id, out v)) { cache.Remove(id); v.Dispose(); }
                miniaturas.Olvidar(id);
            }
            grid.Invalidate();
        }

        // ------------------------------------------------------------ destino

        void ActualizarDestino()
        {
            lblDestino.Text = L.T("Guardar en:  ", "Save to:  ") + biblioteca.Destino;
        }

        void CambiarDestino()
        {
            if (copiando) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = L.T("Elige la carpeta donde se guardarán las fotos y videos del iPhone",
                                      "Choose the folder where your iPhone photos and videos will be saved");
                dlg.ShowNewFolderButton = true;
                if (Directory.Exists(biblioteca.Destino)) dlg.SelectedPath = biblioteca.Destino;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                biblioteca.Abrir(dlg.SelectedPath);
                if (!destinoTemporal) Config.GuardarDestino(dlg.SelectedPath);
            }
            foreach (var m in grid.Secciones.SelectMany(x => x.Items)) m.Imported = biblioteca.YaImportado(m);
            ActualizarDestino();
            AplicarFiltro();
            ActualizarEstado();
            ActualizarSeleccion();
            meses.Invalidate();
        }

        void AbrirCarpeta()
        {
            try
            {
                Directory.CreateDirectory(biblioteca.Destino);
                Process.Start("explorer.exe", "\"" + biblioteca.Destino + "\"");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
        }

        // ------------------------------------------------------------ copiar

        void Copiar()
        {
            if (copiando) { detener = true; ActualizarSeleccion(); return; }
            var sel = Seleccionadas();
            if (sel.Count == 0) return;
            if (Demo.Activo)
            {
                MessageBox.Show(this, L.T("Modo demo: las fotos son de ejemplo y no se copia nada.", "Demo mode: these are sample photos and nothing is copied."), Text);
                return;
            }
            string destino = biblioteca.Destino;
            try { Directory.CreateDirectory(destino); }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("No se puede guardar en ", "Can't save to ") + destino + ":\n\n" + ex.Message + "\n\n" +
                                L.T("Elige otra carpeta con «Cambiar carpeta».", "Choose another folder with “Change folder”."),
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool conLive = chkLive.Checked;
            var trabajos = new List<KeyValuePair<MediaItem, MediaItem>>();   // (foto que se ve, archivo a copiar)
            foreach (var m in sel.OrderBy(x => x.DateUtc ?? DateTime.MinValue))
            {
                trabajos.Add(new KeyValuePair<MediaItem, MediaItem>(m, m));
                if (conLive && m.Companion != null) trabajos.Add(new KeyValuePair<MediaItem, MediaItem>(m, m.Companion));
            }
            long total = trabajos.Sum(t => t.Value.Size);
            try
            {
                var disco = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destino)));
                if (disco.AvailableFreeSpace < total + 100L * 1024 * 1024)
                {
                    MessageBox.Show(this, L.T("No hay espacio suficiente en ", "Not enough space on ") + disco.Name + "\n\n" +
                                    L.T("Necesitas ", "You need ") + Formato.Tamano(total) + L.T(" y hay ", " and there's ") +
                                    Formato.Tamano(disco.AvailableFreeSpace) + L.T(" libres.", " free."),
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch { }

            copiando = true; detener = false;
            grid.Bloqueada = true; grid.Invalidate();
            btnActualizar.Enabled = false; cmbMostrar.Enabled = false; chkOcultar.Enabled = false; chkLive.Enabled = false;
            lnkCambiar.Enabled = false;
            barra.Value = 0; barra.Visible = true; lblProgreso.Visible = true;
            lblProgreso.Text = L.T("Preparando…", "Getting ready…");
            ActualizarSeleccion();

            var hilo = new Thread(() =>
            {
                var reloj = Stopwatch.StartNew();
                long hechos = 0;
                int bien = 0, fallos = 0;
                bool desconectado = false;
                string primerError = null;
                var conFallo = new HashSet<MediaItem>();
                for (int i = 0; i < trabajos.Count; i++)
                {
                    if (detener) break;
                    var dueno = trabajos[i].Key;
                    var archivo = trabajos[i].Value;
                    int n = i + 1; long h = hechos; var transcurrido = reloj.Elapsed;
                    EnUI(() => MostrarProgreso(n, trabajos.Count, h, total, transcurrido, archivo.Name));

                    string error = CopiarUno(archivo);
                    if (error != null && Lector.TelefonoConectado())
                    {
                        // Lo más común: el iPhone se bloqueó y ya no deja leer. Esperamos a que lo desbloqueen.
                        EnUI(() =>
                        {
                            lblProgreso.Text = L.T("El iPhone no deja leer las fotos: desbloquéalo para seguir copiando…",
                                                   "The iPhone won't let the photos be read: unlock it to keep copying…") + "\n" + archivo.Name;
                        });
                        var limite = DateTime.UtcNow.AddMinutes(3);
                        while (error != null && !detener && DateTime.UtcNow < limite)
                        {
                            Thread.Sleep(3000);
                            if (!Lector.TelefonoConectado()) break;
                            error = CopiarUno(archivo);
                        }
                    }
                    hechos += archivo.Size;
                    if (error != null)
                    {
                        fallos++;
                        conFallo.Add(dueno);
                        if (primerError == null) primerError = archivo.Name + ": " + error;
                        if (!Lector.TelefonoConectado()) { desconectado = true; break; }
                        if (detener) break;
                    }
                    bool ultimo = i + 1 >= trabajos.Count || trabajos[i + 1].Key != dueno;
                    if (ultimo)
                    {
                        bool ok = !conFallo.Contains(dueno);
                        if (ok) bien++;
                        EnUI(() =>
                        {
                            if (ok) { dueno.Imported = true; dueno.Selected = false; }
                            grid.Invalidate(); meses.Invalidate(); ActualizarSeleccion();
                        });
                    }
                }
                bool detenido = detener;
                var duracion = reloj.Elapsed;
                EnUI(() => TerminarCopia(bien, conFallo.Count, desconectado, detenido, duracion, primerError));
            }) { IsBackground = true, Name = "Copia" };
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
        }

        string CopiarUno(MediaItem archivo)
        {
            try { return biblioteca.Copiar(archivo); }
            catch (Exception ex) { return ex.Message; }
        }

        void MostrarProgreso(int n, int total, long hechos, long bytesTotal, TimeSpan transcurrido, string nombre)
        {
            barra.Value = (int)Math.Min(1000, hechos * 1000 / Math.Max(1, bytesTotal));
            string resto = "";
            if (transcurrido.TotalSeconds > 3 && hechos > 0)
            {
                double velocidad = hechos / transcurrido.TotalSeconds;
                var queda = Formato.Duracion(TimeSpan.FromSeconds((bytesTotal - hechos) / velocidad));
                resto = "  ·  " + L.T("faltan ", "") + queda + L.T("", " left");
            }
            lblProgreso.Text = L.T("Copiando ", "Copying ") + Formato.Numero(n) + L.T(" de ", " of ") + Formato.Numero(total) + "  ·  " +
                               Formato.Tamano(hechos) + L.T(" de ", " of ") + Formato.Tamano(bytesTotal) + resto + "\n" + nombre;
        }

        void TerminarCopia(int bien, int fallos, bool desconectado, bool detenido, TimeSpan duracion, string primerError)
        {
            copiando = false; detener = false;
            biblioteca.LimpiarTemporal();
            grid.Bloqueada = false;
            btnActualizar.Enabled = !leyendo; cmbMostrar.Enabled = true; chkOcultar.Enabled = true; chkLive.Enabled = true;
            lnkCambiar.Enabled = true;
            barra.Visible = false;
            string msg = (detenido ? L.T("Detenido. ", "Stopped. ") : L.T("Listo: ", "Done: ")) + Formato.Numero(bien) +
                         (bien == 1 ? L.T(" copiada", " copied") : L.T(" copiadas", " copied")) + L.T(" en ", " in ") + Formato.Duracion(duracion) + ".";
            if (fallos > 0)
                msg += "  " + Formato.Numero(fallos) +
                       L.T(" no se pudieron copiar (siguen seleccionadas para reintentar). Revisa que el iPhone esté desbloqueado.\nMotivo: ",
                           " couldn't be copied (they're still selected so you can retry). Make sure the iPhone is unlocked.\nReason: ") + primerError;
            if (desconectado)
                msg = L.T("Se desconectó el iPhone. ", "The iPhone was disconnected. ") + msg +
                      L.T(" Vuelve a conectarlo y toca «Volver a leer».", " Reconnect it and click “Reload”.");
            lblProgreso.Text = msg;
            AplicarFiltro();
            ActualizarEstado();
            ActualizarSeleccion();
            meses.Invalidate();
            if (AlTerminarCopia != null) AlTerminarCopia(this, msg);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (copiando && e.CloseReason == CloseReason.UserClosing &&
                MessageBox.Show(this, L.T("Todavía se está copiando. ¿Quieres detener y salir?\n\nLo que ya se copió queda guardado.",
                                          "Still copying. Do you want to stop and exit?\n\nEverything copied so far is kept."),
                                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            if (lector != null) lector.Cancelar = true;
            detener = true;
            base.OnFormClosing(e);
        }

        // ------------------------------------------------------------ ver en grande (doble clic)

        static string CarpetaVistaPrevia { get { return Path.Combine(Path.GetTempPath(), "ImportarIPhone"); } }

        static void LimpiarVistaPrevia()
        {
            try { if (Directory.Exists(CarpetaVistaPrevia)) Directory.Delete(CarpetaVistaPrevia, true); } catch { }
        }

        void VerEnGrande(MediaItem m)
        {
            if (Demo.Activo) return;
            string enPC = biblioteca.RutaEnPC(m);
            if (enPC != null) { Abrir(enPC); return; }
            lblEstado.Text = L.T("Abriendo ", "Opening ") + m.Name + "…";
            var hilo = new Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(CarpetaVistaPrevia);
                    string ruta = Path.Combine(CarpetaVistaPrevia, m.Name);
                    var fi = new FileInfo(ruta);
                    if (!fi.Exists || fi.Length != m.Size)
                    {
                        if (fi.Exists) fi.Delete();
                        IShellItem it = Shell.FromIdList(m.Idl);
                        try { Shell.CopyTo(it, CarpetaVistaPrevia, m.Name); }
                        finally { Marshal.ReleaseComObject(it); }
                    }
                    EnUI(() => { ActualizarEstado(); Abrir(ruta); });
                }
                catch (Exception ex)
                {
                    EnUI(() => lblEstado.Text = L.T("No se pudo abrir ", "Couldn't open ") + m.Name +
                                                L.T(" (¿el iPhone está bloqueado? desbloquéalo y vuelve a intentar): ",
                                                    " (is the iPhone locked? unlock it and try again): ") + ex.Message);
                }
            }) { IsBackground = true };
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
        }

        void Abrir(string ruta)
        {
            try { Process.Start(ruta); }
            catch (Exception ex) { MessageBox.Show(this, L.T("Windows no pudo abrir el archivo:\n\n", "Windows couldn't open the file:\n\n") + ex.Message, Text); }
        }
    }

    class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    }

    // Preferencias guardadas en %APPDATA%\ImportarIPhone
    static class Config
    {
        static string Carpeta { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImportarIPhone"); } }

        public static string LeerDestino()
        {
            string d = Leer("destino.txt");
            if (!string.IsNullOrEmpty(d)) return d;
            // Por defecto: Imágenes\iPhone. Si Imágenes está dentro de OneDrive, usamos la carpeta local
            // para no subir gigas de fotos a la nube sin querer.
            string imagenes = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (imagenes.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0)
                imagenes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
            return Path.Combine(imagenes, "iPhone");
        }

        public static void GuardarDestino(string d) { Guardar("destino.txt", d); }
        public static bool LeerLive() { return Leer("live.txt") != "no"; }
        public static void GuardarLive(bool v) { Guardar("live.txt", v ? "si" : "no"); }
        public static string LeerAvisoOculto() { return Leer("aviso-oculto.txt"); }
        public static void GuardarAvisoOculto(string mes) { Guardar("aviso-oculto.txt", mes); }
        public static string LeerIdioma() { return Leer("idioma.txt"); }
        public static void GuardarIdioma(string idioma) { Guardar("idioma.txt", idioma); }

        static string Leer(string archivo)
        {
            try { string f = Path.Combine(Carpeta, archivo); return File.Exists(f) ? File.ReadAllText(f).Trim() : null; } catch { return null; }
        }

        static void Guardar(string archivo, string valor)
        {
            try { Directory.CreateDirectory(Carpeta); File.WriteAllText(Path.Combine(Carpeta, archivo), valor); } catch { }
        }
    }
}
