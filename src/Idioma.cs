using System;
using System.Globalization;

namespace ImportarIPhone
{
    // Idioma de la interfaz: español o inglés.
    // Cada texto se escribe en los dos idiomas donde se usa:  L.T("Copiar", "Copy")
    public static class L
    {
        public static bool En { get; private set; }

        public static string T(string espanol, string ingles) { return En ? ingles : espanol; }

        public static CultureInfo Cultura
        {
            get { return En ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("es-MX"); }
        }

        // forzado: "es" / "en" desde la línea de comandos (pruebas). Si no, lo guardado; si no, el idioma de Windows.
        public static void Iniciar(string forzado)
        {
            string guardado = forzado ?? Config.LeerIdioma();
            if (guardado == "es") En = false;
            else if (guardado == "en") En = true;
            else En = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "es";
        }

        public static void Cambiar(bool ingles)
        {
            En = ingles;
            Config.GuardarIdioma(ingles ? "en" : "es");
        }
    }
}
