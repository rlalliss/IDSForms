using PdfApp.Api.Models;

public interface IPdfFieldDiscovery
{
    /// Extracts AcroForm fields from a PDF template and returns normalized metadata.
    Task<IReadOnlyList<FormFieldInfo>> GetFormFieldsAsync(string pdfPath, CancellationToken ct = default);
}
