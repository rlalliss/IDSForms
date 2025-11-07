using iText.Kernel.Pdf;
using iText.Forms;
using iText.IO.Image;
using iText.Kernel.Pdf.Canvas;

using System.Text.RegularExpressions;
using iText.Kernel.Geom;

public interface ISignatureService
{
    string StampSignaturePngIntoField(string srcPdf, string destPdf, string fieldName, string dataUrl);
}

public sealed class SignatureService : ISignatureService
{

    public string StampSignaturePngIntoField(string srcPdf, string destPdf, string fieldName, string dataUrl)
    {
        var match = Regex.Match(dataUrl, @"^data:image/\w+;base64,(.+)$");
        if (!match.Success) throw new InvalidOperationException("Bad signature data URL");
        var pngBytes = Convert.FromBase64String(match.Groups[1].Value);

        using var reader = new PdfReader(srcPdf);
        using var writer = new PdfWriter(destPdf);
        using var pdfDoc = new PdfDocument(reader, writer);

        var form = PdfAcroForm.GetAcroForm(pdfDoc, true);
        var fields = form.GetAllFormFields();
        if (!fields.TryGetValue(fieldName, out var field))
            throw new InvalidOperationException($"Field '{fieldName}' not found");

        var widget = field.GetWidgets()[0];
        var rect = widget.GetRectangle().ToRectangle();
        var page = widget.GetPage();

        var canvas = new PdfCanvas(page);
        var img = new iText.Kernel.Pdf.Xobject.PdfImageXObject(ImageDataFactory.CreatePng(pngBytes));
        var imgW = img.GetWidth();
        var imgH = img.GetHeight();
        var scale = Math.Min(rect.GetWidth() / imgW, rect.GetHeight() / imgH);
        var drawW = imgW * scale;
        var drawH = imgH * scale;
        var x = rect.GetLeft() + (rect.GetWidth() - drawW) / 2;
        var y = rect.GetBottom() + (rect.GetHeight() - drawH) / 2;

        //var rect = new Rectangle(x, y, drawW, drawH);
        canvas.AddXObjectFittedIntoRectangle(img, rect);   // img is PdfImageXObject

        pdfDoc.Close();
        return destPdf;
    }
}
