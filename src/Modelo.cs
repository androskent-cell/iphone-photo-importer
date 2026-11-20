using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace ImportarIPhone
{
    // Una foto o video del iPhone
    public class MediaItem
    {
        static int siguienteId;
        public readonly int Id = Interlocked.Increment(ref siguienteId);
        public string Name;          // IMG_3923.HEIC
        public string Ext;           // .HEIC
        public long Size;
        public DateTime? DateUtc;
        public byte[] Idl;           // dirección del archivo dentro del iPhone
        public string Month;         // 2025-08
        public bool IsVideo;
        public MediaItem Companion;  // el video corto de una Live Photo
        public bool Imported;
        public bool Selected;

        public string Key { get { return Month + "/" + Name + "|" + Size; } }
    }

    public class MonthSection
    {
        public string Key;           // 2025-08
        public string Title { get { return Formato.NombreMes(Key); } }   // Agosto de 2025 / August 2025
        public List<MediaItem> Items = new List<MediaItem>();
        public List<MediaItem> View = new List<MediaItem>();
        public int Top, Height;
        public Rectangle LinkRect;   // en coordenadas del contenido
    }

    public static class Formato
    {
        public static string Tamano(double bytes)
        {
            var c = L.Cultura;
            if (bytes >= 1024.0 * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", c) + " GB";
            if (bytes >= 1024.0 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0", c) + " MB";
            return Math.Max(1, Math.Round(bytes / 1024.0)).ToString("0", c) + " KB";
        }

        public static string Numero(long n) { return n.ToString("N0", L.Cultura); }

        public static string Duracion(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return L.T("menos de 1 min", "under 1 min");
            if (t.TotalHours < 1) return (int)Math.Ceiling(t.TotalMinutes) + " min";
            return (int)t.TotalHours + " h " + t.Minutes + " min";
        }

        // 11 de noviembre de 2026 / November 11, 2026
        public static string FechaLarga(DateTime local)
        {
            return local.ToString(L.T("d 'de' MMMM 'de' yyyy", "MMMM d, yyyy"), L.Cultura);
        }

        // "202508_a" -> "2025-08"
        public static string MesDeCarpeta(string carpeta)
        {
            var m = Regex.Match(carpeta ?? "", @"^(\d{4})(\d{2})_");
            if (m.Success) return m.Groups[1].Value + "-" + m.Groups[2].Value;
            return string.IsNullOrEmpty(carpeta) ? "Sin fecha" : carpeta;
        }

        static bool Fecha(string clave, out DateTime d)
        {
            return DateTime.TryParseExact(clave + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        static string Mayuscula(string s) { return s.Length == 0 ? s : char.ToUpper(s[0], L.Cultura) + s.Substring(1); }

        // "2025-08" -> "Agosto de 2025" / "August 2025"
        public static string NombreMes(string clave)
        {
            DateTime d;
            return Fecha(clave, out d) ? Mayuscula(d.ToString(L.T("MMMM 'de' yyyy", "MMMM yyyy"), L.Cultura)) : clave;
        }

        // "2025-08" -> "Agosto 2025" / "August 2025"
        public static string NombreMesCorto(string clave)
        {
            DateTime d;
            return Fecha(clave, out d) ? Mayuscula(d.ToString("MMMM yyyy", L.Cultura)) : clave;
        }
    }

    // La carpeta de destino y el registro de lo que ya se pasó a la PC.
    // El registro (.importados.txt) es el mismo formato que usaba el script de PowerShell.
    public class Biblioteca
    {
        readonly object candado = new object();
        HashSet<string> registro = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string Destino { get; private set; }

        string RutaRegistro { get { return Path.Combine(Destino, ".importados.txt"); } }

        public void Abrir(string destino)
        {
            lock (candado)
            {
                Destino = destino;
                registro = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(RutaRegistro))
                        foreach (var linea in File.ReadAllLines(RutaRegistro)) registro.Add(linea.Trim());
                }
                catch { }
            }
        }

        public bool YaImportado(MediaItem m)
        {
            string destino;
            lock (candado)
            {
                if (registro.Contains(m.Key)) return true;
                destino = Destino;
            }
            try
            {
                var f = new FileInfo(Path.Combine(destino, m.Month, m.Name));
                return f.Exists && f.Length == m.Size;
            }
            catch { return false; }
        }

        void Registrar(MediaItem m)
        {
            lock (candado)
            {
                if (!registro.Add(m.Key)) return;
                bool nuevo = !File.Exists(RutaRegistro);
                File.AppendAllText(RutaRegistro, m.Key + "\r\n");
                if (nuevo) File.SetAttributes(RutaRegistro, FileAttributes.Hidden);
            }
        }

        public string RutaEnPC(MediaItem m)
        {
            var f = new FileInfo(Path.Combine(Destino, m.Month, m.Name));
            return f.Exists && f.Length == m.Size ? f.FullName : null;
        }

        // Copia un archivo del iPhone a Destino\AAAA-MM\. Devuelve null si salió bien, o el motivo si falló.
        public string Copiar(MediaItem m)
        {
            string carpetaMes = Path.Combine(Destino, m.Month);
            string final = Path.Combine(carpetaMes, m.Name);
            var existente = new FileInfo(final);
            if (existente.Exists && existente.Length == m.Size) { Registrar(m); return null; }

            // Se copia primero a una carpeta oculta: si algo falla, no queda un archivo a medias entre tus fotos
            var temporal = Directory.CreateDirectory(Path.Combine(Destino, ".importando"));
            temporal.Attributes |= FileAttributes.Hidden;
            foreach (var f in temporal.GetFiles())
            {
                try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { }
            }

            IShellItem item = Shell.FromIdList(m.Idl);
            try { Shell.CopyTo(item, temporal.FullName, m.Name); }
            finally { Marshal.ReleaseComObject(item); }

            var copiado = new FileInfo(Path.Combine(temporal.FullName, m.Name));
            if (!copiado.Exists) return L.T("no llegó a la PC", "never reached the PC");
            if (m.Size > 0 && copiado.Length != m.Size)
            {
                try { copiado.Delete(); } catch { }
                return L.T("quedó incompleto", "the copy was incomplete");
            }

            Directory.CreateDirectory(carpetaMes);
            final = RutaLibre(final);
            File.Move(copiado.FullName, final);
            if (m.DateUtc.HasValue)
            {
                // Que en el Explorador salga la fecha en que se tomó, no la de hoy
                try
                {
                    File.SetCreationTimeUtc(final, m.DateUtc.Value);
                    File.SetLastWriteTimeUtc(final, m.DateUtc.Value);
                }
                catch { }
            }
            Registrar(m);
            return null;
        }

        // Borra la carpeta temporal oculta al terminar de copiar
        public void LimpiarTemporal()
        {
            try
            {
                var t = new DirectoryInfo(Path.Combine(Destino, ".importando"));
                if (!t.Exists) return;
                foreach (var f in t.GetFiles()) { f.Attributes = FileAttributes.Normal; f.Delete(); }
                t.Delete();
            }
            catch { }
        }

        static string RutaLibre(string ruta)
        {
            if (!File.Exists(ruta)) return ruta;
            string dir = Path.GetDirectoryName(ruta), nombre = Path.GetFileNameWithoutExtension(ruta), ext = Path.GetExtension(ruta);
            for (int n = 2; ; n++)
            {
                string otra = Path.Combine(dir, nombre + " (" + n + ")" + ext);
                if (!File.Exists(otra)) return otra;
            }
        }
    }
}
