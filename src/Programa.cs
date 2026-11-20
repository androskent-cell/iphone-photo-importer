using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImportarIPhone
{
    static class Programa
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            L.Iniciar(Arg(args, "--idioma"));
            Application.ThreadException += (o, e) =>
                MessageBox.Show(L.T("Ocurrió un error inesperado:\n\n", "An unexpected error occurred:\n\n") + e.Exception.Message,
                                Ventana.NombreApp, MessageBoxButtons.OK, MessageBoxIcon.Error);

            DateTime hoy;
            if (DateTime.TryParse(Arg(args, "--hoy"), System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.AdjustToUniversal, out hoy))
                Ventana.HoyDePrueba = hoy;
            Demo.Activo = args.Contains("--demo");
            var v = new Ventana(Arg(args, "--destino"));
            string captura = Arg(args, "--captura");
            if (captura != null) Pruebas.Configurar(v, captura, args.Contains("--autoprueba"));
            Application.Run(v);
        }

        static string Arg(string[] args, string nombre)
        {
            int i = Array.IndexOf(args, nombre);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }

    // Modo de prueba (no lo usa nadie en el día a día): abre, espera a que cargue, guarda una captura y cierra.
    static class Pruebas
    {
        public static void Configurar(Ventana v, string png, bool copiar)
        {
            Cuadricula.Privado = !Demo.Activo;   // con fotos reales, la captura solo guarda colores
            bool empezado = false;
            v.AlCargarMes = w =>
            {
                if (empezado || w.MesesCargados < 3) return;
                empezado = true;
                Despues(Demo.Activo ? 3000 : 7000, () =>
                {
                    if (!copiar) { Capturar(w, png); w.Close(); return; }
                    w.SeleccionarPrimeros(4);
                    Capturar(w, Path.ChangeExtension(png, null) + "-seleccion.png");
                    w.CopiarAhora();
                });
            };
            if (copiar)
                v.AlTerminarCopia = (w, msg) => Despues(1500, () =>
                {
                    Capturar(w, png);
                    File.WriteAllText(png + ".txt", msg);
                    w.Close();
                });
        }

        static void Despues(int ms, Action a)
        {
            var t = new Timer { Interval = ms };
            t.Tick += (o, e) => { t.Stop(); t.Dispose(); a(); };
            t.Start();
        }

        static void Capturar(Form f, string png)
        {
            using (var b = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                b.Save(png, ImageFormat.Png);
            }
        }
    }
}
