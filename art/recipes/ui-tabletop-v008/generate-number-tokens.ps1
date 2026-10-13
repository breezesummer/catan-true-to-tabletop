param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../../../Unity/Catan/Assets/Resources/TabletopUI/v008/NumberTokens'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
foreach ($number in @(2,3,4,5,6,8,9,10,11,12)) {
    $bitmap = [System.Drawing.Bitmap]::new(512, 512)
    $canvas = [System.Drawing.Graphics]::FromImage($bitmap)
    $canvas.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $canvas.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $canvas.Clear([System.Drawing.Color]::FromArgb(255, 246, 228, 181))
    $font = [System.Drawing.Font]::new('Arial', 304, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $ink = if ($number -eq 6 -or $number -eq 8) { [System.Drawing.Color]::FromArgb(255, 246, 30, 20) } else { [System.Drawing.Color]::FromArgb(255, 32, 24, 16) }
    $brush = [System.Drawing.SolidBrush]::new($ink)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $canvas.DrawString($number.ToString(), $font, $brush, [System.Drawing.RectangleF]::new(0, -6, 512, 512), $format)
    $bitmap.Save((Join-Path $OutputDirectory ('number-' + $number + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $format.Dispose(); $brush.Dispose(); $font.Dispose(); $canvas.Dispose(); $bitmap.Dispose()
}
# 只有数字贴图是位图；圆形牌芯与厚度由 TabletopNumberTokens.cs 生成真实米制网格。
