using iText.Kernel.Pdf;

public interface IPdfFillService {
  Task<string> FillAsync(string templatePath, IDictionary<string,string> values, bool flatten, CancellationToken ct);
}

public sealed class PdfFillService : IPdfFillService {
  private readonly IWebHostEnvironment _env;
  public PdfFillService(IWebHostEnvironment env) => _env = env;

  public Task<string> FillAsync(string templatePath, IDictionary<string,string> values, bool flatten, CancellationToken ct) {
    var outDir = Path.Combine(_env.ContentRootPath, "filled");
    Directory.CreateDirectory(outDir);
    var outPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(templatePath)}_{Guid.NewGuid():N}.pdf");

    using var reader = new iText.Kernel.Pdf.PdfReader(templatePath);
    using var writer = new iText.Kernel.Pdf.PdfWriter(outPath);
    using var pdf = new iText.Kernel.Pdf.PdfDocument(reader, writer);
    var form = iText.Forms.PdfAcroForm.GetAcroForm(pdf, true);
    form.SetNeedAppearances(true);

    var fields = form.GetAllFormFields();
    foreach (var kv in values)
      if (fields.TryGetValue(kv.Key, out var f)) { f.SetDefaultValue(new PdfString(kv.Value ?? "")); f.SetValue(kv.Value ?? ""); }

    var today = DateTime.Now.ToString("MM/dd/yyyy");
    foreach (var entry in fields)
    {
      if (entry.Key.Contains("date", StringComparison.OrdinalIgnoreCase))
      {
        var current = entry.Value.GetValueAsString();
        if (string.IsNullOrWhiteSpace(current))
        {
          entry.Value.SetDefaultValue(new PdfString(today));
          entry.Value.SetValue(today);
        }
      }
    }

    if (flatten) form.FlattenFields();
    pdf.Close();
    return Task.FromResult(outPath);
  }
}
