# Dibuja el ícono de la app (icono.ico) con varias resoluciones en formato PNG.
Add-Type -AssemblyName System.Drawing
$salida = Join-Path $PSScriptRoot 'icono.ico'

function Dibujar([int]$n) {
    $b = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $f = $n / 256.0
    # Fondo redondeado con degradado azul
    $r = New-Object System.Drawing.RectangleF (8 * $f), (8 * $f), (240 * $f), (240 * $f)
    $d = 104 * $f
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90); $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90); $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90); $p.CloseFigure()
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $r, ([System.Drawing.Color]::FromArgb(64, 156, 255)), ([System.Drawing.Color]::FromArgb(0, 98, 230)), 90
    $g.FillPath($grad, $p)
    # Foto: marco blanco con montañas y sol
    $blanco = [System.Drawing.Brushes]::White
    $marco = New-Object System.Drawing.RectangleF (52 * $f), (62 * $f), (152 * $f), (116 * $f)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), (12 * $f)
    $pen.LineJoin = 'Round'
    $g.DrawRectangle($pen, $marco.X, $marco.Y, $marco.Width, $marco.Height)
    $g.FillEllipse($blanco, 150 * $f, 82 * $f, 28 * $f, 28 * $f)
    $mont = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF (60 * $f), (170 * $f)),
        (New-Object System.Drawing.PointF (104 * $f), (112 * $f)),
        (New-Object System.Drawing.PointF (132 * $f), (146 * $f)),
        (New-Object System.Drawing.PointF (152 * $f), (126 * $f)),
        (New-Object System.Drawing.PointF (196 * $f), (170 * $f)))
    $g.FillPolygon($blanco, $mont)
    # Flecha hacia abajo (pasar a la PC)
    $flecha = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF (114 * $f), (186 * $f)),
        (New-Object System.Drawing.PointF (142 * $f), (186 * $f)),
        (New-Object System.Drawing.PointF (142 * $f), (204 * $f)),
        (New-Object System.Drawing.PointF (162 * $f), (204 * $f)),
        (New-Object System.Drawing.PointF (128 * $f), (232 * $f)),
        (New-Object System.Drawing.PointF (94 * $f), (204 * $f)),
        (New-Object System.Drawing.PointF (114 * $f), (204 * $f)))
    $g.FillPolygon($blanco, $flecha)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    return ,$ms.ToArray()
}

$tamanos = 16, 24, 32, 48, 64, 256
$imagenes = foreach ($t in $tamanos) { ,(Dibujar $t) }
$fs = [System.IO.File]::Create($salida)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $datos = $imagenes[$i]
    $w.Write([byte]($t % 256)); $w.Write([byte]($t % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$datos.Length); $w.Write([uint32]$offset)
    $offset += $datos.Length
}
foreach ($datos in $imagenes) { $w.Write($datos) }
$w.Close()
"Icono creado: $salida"
