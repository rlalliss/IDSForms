using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using PdfApp.Api.Models;

public sealed class PdfFieldDiscovery : IPdfFieldDiscovery
{
    public Task<IReadOnlyList<FormFieldInfo>> GetFormFieldsAsync(string pdfPath, CancellationToken ct = default)
    {
        var results = new List<FormFieldInfo>();

        using var reader = new PdfReader(pdfPath);
        using var pdfDoc = new PdfDocument(reader);

        var acro = PdfAcroForm.GetAcroForm(pdfDoc, false);
        if (acro == null)
            return Task.FromResult<IReadOnlyList<FormFieldInfo>>(results);

        var fields = acro.GetAllFormFields();

        foreach (var kv in fields)
        {
            var name = kv.Key;
            var field = kv.Value;
            var type = MapType(field);
            var required = SafeIsRequired(field);

            foreach (var widget in field.GetWidgets())
            {
                var rect = widget.GetRectangle().ToRectangle();
                var page = widget.GetPage();
                var pageNum = page?.GetDocument()?.GetPageNumber(page) ?? 1;

                results.Add(new FormFieldInfo
                {
                    Name = name,
                    Type = type,
                    Required = required,
                    PageNumber = pageNum,
                    X = rect.GetLeft(),
                    Y = rect.GetBottom(),
                    Width = rect.GetWidth(),
                    Height = rect.GetHeight()
                });
            }
        }

        results = results
            .OrderBy(r => r.PageNumber)
            .ThenByDescending(r => r.Y)
            .ThenBy(r => r.X)
            .ToList();

        return Task.FromResult<IReadOnlyList<FormFieldInfo>>(results);
    }

    // Helper methods
    private static string MapType(PdfFormField f)
    {
        return f switch
        {
            PdfButtonFormField btn when btn.IsPushButton() => "button",
            PdfButtonFormField btn when btn.IsRadio() => "radio",
            PdfButtonFormField => "checkbox",
            PdfSignatureFormField => "signature",
            PdfTextFormField txt when IsMultiline(txt) => "textarea",
            PdfTextFormField => "text",
            PdfChoiceFormField choice when choice.IsCombo() => "combo",
            PdfChoiceFormField => "list",
            _ => "unknown"
        };
    }

    private static bool IsMultiline(PdfTextFormField f)
    {
        try { return f.IsMultiline(); } catch { return (f.GetFieldFlags() & 4096) == 4096; }
    }

    private static bool SafeIsRequired(PdfFormField f)
    {
        try { return f.IsRequired(); } catch { return (f.GetFieldFlags() & 1) == 1; }
    }
}
