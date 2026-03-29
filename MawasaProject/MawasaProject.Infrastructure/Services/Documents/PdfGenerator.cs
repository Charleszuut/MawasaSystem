using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class PdfGenerator
{
    public async Task<string> SaveAsPdfAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);

        await Task.Run(() =>
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            
            var font = new XFont("Consolas", 11, XFontStyleEx.Regular);
            var format = XStringFormats.TopLeft;

            double yPoint = 40;
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                gfx.DrawString(line.TrimEnd('\r'), font, XBrushes.Black, new XRect(40, yPoint, page.Width, page.Height), format);
                yPoint += 14; 
            }

            document.Save(path);
        }, cancellationToken);

        return path;
    }
}
