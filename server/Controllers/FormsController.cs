using System.Security.Claims;
using System.Text.Json;
using iText.Forms;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController, Route("api/forms")]
public sealed class FormsController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IPdfFillService _pdf;
    private readonly IEmailService _email;
    private readonly ISignatureService _signature;  // 👈 this is the variable you're asking about
    private readonly IWebHostEnvironment _env;
    private readonly IStorageService _storage;

    public FormsController(AppDb db, IPdfFillService pdf, IEmailService email, IWebHostEnvironment env, ISignatureService signature, IStorageService storage)
      => (_db, _pdf, _email, _env, _signature, _storage) = (db, pdf, email, env, signature, storage);

    [Authorize, HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var query = _db.Forms.Where(f => f.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(f => (f.Title + " " + (f.Keywords ?? "")).Contains(q));
        var list = await query
            .OrderBy(f => f.Title)
            .Select(f => new { f.Slug, f.Title, f.Category, f.Description })
            .ToListAsync();
        return Ok(list);
    }

    public sealed record PrefillReq(Dictionary<string, string>? Customer = null);

    [Authorize, HttpGet("{slug}/prefill")]
    public Task<IActionResult> Prefill(string slug) => PrefillInternal(slug, null);

    [Authorize, HttpPost("{slug}/prefill")]
    public Task<IActionResult> PrefillWithCustomer(string slug, [FromBody] PrefillReq? req)
        => PrefillInternal(slug, req?.Customer);

    private async Task<IActionResult> PrefillInternal(string slug, Dictionary<string, string>? customer)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var prefill = await BuildPrefillAsync(uid, form, customer);
        return Ok(prefill);
    }

    // FormsController.cs
    [Authorize]
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetMeta(string slug)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var fields = await _db.FormFields
            .Where(x => x.FormId == form.Id)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.PdfFieldName, x.Label, x.Type, x.Required, x.OrderIndex })
            .ToListAsync();

        return Ok(new
        {
            form.Slug,
            form.Title,
            form.PdfBlobPath,
            Fields = fields
        });
    }
    [Authorize]
    [HttpGet("{slug}/pdf")]
    public async Task<IActionResult> GetTemplatePdf(string slug)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();
        var path = await _storage.GetLocalPathAsync(form.PdfBlobPath, HttpContext.RequestAborted);
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }

    public sealed record PreviewReq(Dictionary<string, string> Values, Dictionary<string, string>? Customer = null);

    [Authorize]
    [HttpPost("{slug}/preview")]
    public async Task<IActionResult> PreviewFilled(string slug, [FromBody] PreviewReq req)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var values = await BuildPrefillAsync(uid, form, req.Customer);
        var signatureDataUrl = values.TryGetValue("CustomerSignatureDataUrl", out var sig) ? sig : null;
        foreach (var kv in req.Values) values[kv.Key] = kv.Value; // user overrides

        // Create a filled temp file
        var tPath = await _storage.GetLocalPathAsync(form.PdfBlobPath, HttpContext.RequestAborted);
        var path = await _pdf.FillAsync(tPath, values, flatten: false, HttpContext.RequestAborted);
        path = await ApplyCustomerSignatureAsync(form, path, signatureDataUrl);
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        System.IO.File.Delete(path); // temp
        return File(bytes, "application/pdf", $"preview_{slug}.pdf");
    }
    public sealed record SubmitReq(
        Dictionary<string, string> Values,
        bool Flatten = false,
        string? ToOverride = null,
        string? CcOverride = null,
        string? BccOverride = null,
        Dictionary<string, string>? Customer = null
    );

    public sealed class SubmitPdfUploadReq
    {
        public IFormFile? Pdf { get; set; }
        public bool Flatten { get; set; }
        public string? ToOverride { get; set; }
        public string? CcOverride { get; set; }
        public string? BccOverride { get; set; }
        public string? Customer { get; set; }
    }

    // [Authorize, HttpPost("{slug}/submit")]
    // public async Task<IActionResult> Submit(string slug, [FromBody] Dictionary<string, string> input)
    // {
    //     var form = await _db.Forms.Include(f => f.EmailTemplate).FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
    //     if (form is null) return NotFound();

    //     var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    //     var values = await BuildPrefillAsync(uid, form);
    //     foreach (var kv in input) values[kv.Key] = kv.Value; // user overrides

    //     // (Add required-field checks here if you track them.)

    //     var pdfPath = await _pdf.FillAsync(ResolvePdfPath(form.PdfBlobPath), values, flatten: false, HttpContext.RequestAborted);

    //     var tpl = form.EmailTemplate!;
    //     string Render(string s) => values.Aggregate(s, (acc, kv) => acc.Replace("{{" + kv.Key + "}}", kv.Value ?? ""));
    //     var msgId = await _email.SendAsync(Render(tpl.To), Render(tpl.Subject), Render(tpl.BodyHtml), pdfPath);

    //     var sub = new Submission
    //     {
    //         FormId = form.Id,
    //         UserId = uid,
    //         PdfPath = pdfPath,
    //         PayloadJson = System.Text.Json.JsonSerializer.Serialize(values),
    //         EmailMessageId = msgId
    //     };
    //     _db.Submissions.Add(sub);
    //     await _db.SaveChangesAsync();

    //     return Ok(new { pdf = pdfPath, emailMessageId = msgId });
    // }

    [Authorize]
    [HttpPost("{slug}/submit")]
    public async Task<IActionResult> Submit(string slug, [FromBody] SubmitReq req)
    {
        var form = await _db.Forms.Include(f => f.EmailTemplate).FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var values = await BuildPrefillAsync(uid, form, req.Customer);
        var signatureDataUrl = values.TryGetValue("CustomerSignatureDataUrl", out var sig) ? sig : null;
        foreach (var kv in req.Values) values[kv.Key] = kv.Value;

        // (Optional) validate requireds from FormFields here

        var template = await _storage.GetLocalPathAsync(form.PdfBlobPath, HttpContext.RequestAborted);
        var pdfPath = await _pdf.FillAsync(template, values, req.Flatten, HttpContext.RequestAborted);
        pdfPath = await ApplyCustomerSignatureAsync(form, pdfPath, signatureDataUrl);

        var tpl = form.EmailTemplate;
        if (tpl is null)
        {
            System.IO.File.Delete(pdfPath);
            return BadRequest(new { error = "Form is missing an email template configuration." });
        }
        string Render(string s) => values.Aggregate(s, (acc, kv) => acc.Replace("{{" + kv.Key + "}}", kv.Value ?? ""));

        var customerEmails = ExtractCustomerEmails(values);
        var to = string.IsNullOrWhiteSpace(req.ToOverride) ? tpl.To : req.ToOverride!;
        var toList = new[] { to }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Concat(customerEmails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        to = string.Join(",", toList);

        var cc = string.IsNullOrWhiteSpace(req.CcOverride) ? tpl.Cc ?? "" : req.CcOverride!;
        var bcc = string.IsNullOrWhiteSpace(req.BccOverride) ? tpl.Bcc ?? "" : req.BccOverride!;

        var subject = Render(tpl.Subject);
        var body = Render(tpl.BodyHtml);

        var msgId = await _email.SendAsync(to, subject, body, pdfPath);
        if (!string.IsNullOrWhiteSpace(cc)) await _email.SendAsync(cc, "(CC) " + subject, body, pdfPath);
        if (!string.IsNullOrWhiteSpace(bcc)) await _email.SendAsync(bcc, "(BCC) " + subject, body, pdfPath);

        _db.Submissions.Add(new Submission
        {
            FormId = form.Id,
            UserId = uid,
            PdfPath = pdfPath,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(values),
            EmailMessageId = msgId
        });
        await _db.SaveChangesAsync();

        return Ok(new { pdf = pdfPath, emailMessageId = msgId, to, cc, bcc });
    }

    [Authorize]
    [HttpPost("{slug}/submit-pdf")]
    public async Task<IActionResult> SubmitPdf(string slug, [FromForm] SubmitPdfUploadReq req)
    {
        if (req.Pdf is null || req.Pdf.Length == 0) return BadRequest(new { error = "A completed PDF must be uploaded." });

        var form = await _db.Forms.Include(f => f.EmailTemplate).FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        Dictionary<string, string>? customerOverrides = null;
        if (!string.IsNullOrWhiteSpace(req.Customer))
        {
            try
            {
                customerOverrides = JsonSerializer.Deserialize<Dictionary<string, string>>(req.Customer);
            }
            catch
            {
                // ignore malformed payload, treat as missing customer overrides
            }
        }

        var values = await BuildPrefillAsync(uid, form, customerOverrides);

        var uploadsDir = Path.Combine(_env.ContentRootPath, "filled");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{form.Slug}_{Guid.NewGuid():N}.pdf";
        var uploadPath = Path.Combine(uploadsDir, fileName);
        await using (var fs = System.IO.File.Create(uploadPath))
        {
            await req.Pdf.CopyToAsync(fs, HttpContext.RequestAborted);
        }

        if (req.Flatten)
        {
            var flattened = await _pdf.FillAsync(uploadPath, new Dictionary<string, string>(), flatten: true, HttpContext.RequestAborted);
            System.IO.File.Delete(uploadPath);
            uploadPath = flattened;
        }

        var tpl = form.EmailTemplate;
        if (tpl is null)
        {
            System.IO.File.Delete(uploadPath);
            return BadRequest(new { error = "Form is missing an email template configuration." });
        }

        string Render(string s) => values.Aggregate(s, (acc, kv) => acc.Replace("{{" + kv.Key + "}}", kv.Value ?? ""));

        var customerEmails = ExtractCustomerEmails(values);
        var to = string.IsNullOrWhiteSpace(req.ToOverride) ? tpl.To : req.ToOverride!;
        var toList = new[] { to }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Concat(customerEmails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        to = string.Join(",", toList);

        var cc = string.IsNullOrWhiteSpace(req.CcOverride) ? tpl.Cc ?? "" : req.CcOverride!;
        var bcc = string.IsNullOrWhiteSpace(req.BccOverride) ? tpl.Bcc ?? "" : req.BccOverride!;

        var subject = Render(tpl.Subject);
        var body = Render(tpl.BodyHtml);

        var msgId = await _email.SendAsync(to, subject, body, uploadPath);
        if (!string.IsNullOrWhiteSpace(cc)) await _email.SendAsync(cc, "(CC) " + subject, body, uploadPath);
        if (!string.IsNullOrWhiteSpace(bcc)) await _email.SendAsync(bcc, "(BCC) " + subject, body, uploadPath);

        var payload = new
        {
            Mode = "uploadedPdf",
            Email = new { req.ToOverride, req.CcOverride, req.BccOverride, req.Flatten },
            Customer = customerOverrides
        };

        _db.Submissions.Add(new Submission
        {
            FormId = form.Id,
            UserId = uid,
            PdfPath = uploadPath,
            PayloadJson = JsonSerializer.Serialize(payload),
            EmailMessageId = msgId
        });
        await _db.SaveChangesAsync();

        return Ok(new { pdf = uploadPath, emailMessageId = msgId, to, cc, bcc });
    }

    private async Task<Dictionary<string, string>> BuildPrefillAsync(Guid userId, Form form, Dictionary<string, string>? customerFields = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Form defaults (scoped to user + form)
        var fd = await _db.FormDefaults
            .Where(x => x.FormId == form.Id && x.UserId == userId)
            .ToDictionaryAsync(x => x.FieldName, x => x.FieldValue);
        foreach (var kv in fd) result[kv.Key] = kv.Value ?? "";

        // Profile mapping (adjust to your field names)
        var p = await _db.UserProfiles.FindAsync(userId);
        if (p != null)
        {
            void map(string field, string? val) { if (!string.IsNullOrWhiteSpace(val)) result[field] = val!; }
            map("CustomerName", p.FullName);
            map("Dealership", p.Company);
            map("Email", p.Email);
        }

        // User defaults (global)
        var ug = await _db.UserDefaults.Where(x => x.UserId == userId && x.FormSlug == null)
          .ToDictionaryAsync(x => x.FieldName, x => x.FieldValue);
        foreach (var kv in ug) result[kv.Key] = kv.Value ?? "";

        // User defaults (per form)
        var uf = await _db.UserDefaults.Where(x => x.UserId == userId && x.FormSlug == form.Slug)
          .ToDictionaryAsync(x => x.FieldName, x => x.FieldValue);
        foreach (var kv in uf) result[kv.Key] = kv.Value ?? "";

        // Customer overrides (from UI quick customer panel)
        if (customerFields is not null)
        {
            foreach (var kv in customerFields)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                result[kv.Key] = kv.Value ?? "";
            }
        }

        //var signedPath = Path.Combine(outDir, "filled_signed.pdf");
        //_signature.StampSignaturePngIntoField(pdfPath, signedPath, "CustomerSignature", req.CustomerSignatureDataUrl);

        return result;
    }

    private async Task<string> ApplyCustomerSignatureAsync(Form form, string pdfPath, string? signatureDataUrl)
    {
        if (string.IsNullOrWhiteSpace(signatureDataUrl)) return pdfPath;

        var signatureFields = await ResolveCustomerSignatureFieldsAsync(form, pdfPath);

        if (signatureFields.Count == 0) return pdfPath;

        var directory = Path.GetDirectoryName(pdfPath);
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetTempPath();
        var invalidChars = Path.GetInvalidFileNameChars();

        string CleanSegment(string name)
            => new string(name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

        var workingPath = pdfPath;
        foreach (var fieldName in signatureFields)
        {
            var safeField = CleanSegment(fieldName);
            var destPath = Path.Combine(directory!, $"{Path.GetFileNameWithoutExtension(pdfPath)}_{safeField}_{Guid.NewGuid():N}.pdf");
            var stampedPath = _signature.StampSignaturePngIntoField(workingPath, destPath, fieldName, signatureDataUrl);
            if (!string.Equals(workingPath, pdfPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(workingPath))
            {
                System.IO.File.Delete(workingPath);
            }
            workingPath = stampedPath;
        }

        if (!string.Equals(workingPath, pdfPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(pdfPath))
        {
            System.IO.File.Delete(pdfPath);
        }

        return workingPath;
    }

    private static List<string> ExtractCustomerEmails(Dictionary<string, string> values)
    {
        var list = new List<string>();
        if (values.TryGetValue("CustomerEmail", out var email) && !string.IsNullOrWhiteSpace(email))
        {
            list.AddRange(email.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        if (values.TryGetValue("CustomerSecondaryEmail", out var email2) && !string.IsNullOrWhiteSpace(email2))
        {
            list.AddRange(email2.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        return list.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<List<string>> ResolveCustomerSignatureFieldsAsync(Form form, string pdfPath)
    {
        var dbFields = await _db.FormFields
            .Where(f => f.FormId == form.Id && f.PdfFieldName.Contains("CustomerSignature"))
            .OrderBy(f => f.OrderIndex)
            .Select(f => f.PdfFieldName)
            .ToListAsync();
        if (dbFields.Count > 0) return dbFields;

        try
        {
            using var reader = new PdfReader(pdfPath);
            using var pdfDoc = new PdfDocument(reader);
            var acro = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (acro is null) return new List<string>();
            var names = acro.GetAllFormFields()
                .Keys
                .Where(k => k.Contains("CustomerSignature", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return names;
        }
        catch
        {
            return new List<string>();
        }
    }

    // Path resolution handled by IStorageService

    [Authorize]
    [HttpGet("{slug}/fields")]
    public async Task<IActionResult> GetFormFields(string slug, [FromServices] IPdfFieldDiscovery discovery)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var path = await _storage.GetLocalPathAsync(form.PdfBlobPath, HttpContext.RequestAborted);
        var items = await discovery.GetFormFieldsAsync(path, HttpContext.RequestAborted);

        // If you also store FormFields in DB with friendly labels, you can enrich here:
        var labels = await _db.FormFields
            .Where(x => x.FormId == form.Id)
            .ToDictionaryAsync(x => x.PdfFieldName, x => new { x.Label, x.Type, x.Required });

        var shaped = items.Select(i => new
        {
            name = i.Name,
            label = labels.TryGetValue(i.Name, out var meta) ? meta.Label : i.Name,
            type = labels.TryGetValue(i.Name, out meta) ? meta.Type : i.Type,
            required = labels.TryGetValue(i.Name, out meta) ? meta.Required : i.Required,
            page = i.PageNumber,
            x = i.X,
            y = i.Y,
            width = i.Width,
            height = i.Height
        });

        return Ok(shaped);
    }

}
