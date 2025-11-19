using System.Security.Claims;
using iText.Forms;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authorization;
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

    // Signing workflow
    public sealed record StartSigningReq(
        Dictionary<string, string> Values,
        bool Flatten = true,
        string? ToOverride = null,
        string? CcOverride = null,
        string? BccOverride = null,
        Dictionary<string, string>? Customer = null
    );

    public sealed record CaptureSignatureReq(
        Guid SignatureRequirementId,
        string DataUrl
    );

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

        var to = string.IsNullOrWhiteSpace(req.ToOverride) ? tpl.To : req.ToOverride!;
        var cc = string.IsNullOrWhiteSpace(req.CcOverride) ? tpl.Cc ?? "" : req.CcOverride!;
        var bcc = string.IsNullOrWhiteSpace(req.BccOverride) ? tpl.Bcc ?? "" : req.BccOverride!;

        var subject = Render(tpl.Subject);
        var body = Render(tpl.BodyHtml);

        var msgId = await _email.SendAsync(to, subject, body, pdfPath);
        if (!string.IsNullOrWhiteSpace(cc)) await _email.SendAsync(cc, "(CC) " + subject, body, null);
        if (!string.IsNullOrWhiteSpace(bcc)) await _email.SendAsync(bcc, "(BCC) " + subject, body, null);

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
    [HttpPost("{slug}/signing/start")]
    public async Task<IActionResult> StartSigning(string slug, [FromBody] StartSigningReq req)
    {
        var form = await _db.Forms.Include(f => f.EmailTemplate).FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var values = await BuildPrefillAsync(uid, form, req.Customer);
        foreach (var kv in req.Values) values[kv.Key] = kv.Value;

        var tpath = await _storage.GetLocalPathAsync(form.PdfBlobPath, HttpContext.RequestAborted);
        var filledPath = await _pdf.FillAsync(tpath, values, flatten: false, HttpContext.RequestAborted);

        var submission = new Submission
        {
            FormId = form.Id,
            UserId = uid,
            PdfPath = filledPath,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Values = values,
                Email = new { req.ToOverride, req.CcOverride, req.BccOverride, req.Flatten }
            })
        };
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var reqs = await _db.Set<SignatureRequirement>()
            .Where(r => r.FormId == form.Id)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync();

        foreach (var r in reqs)
            _db.SubmissionSignatures.Add(new SubmissionSignature
            {
                SubmissionId = submission.Id,
                SignatureRequirementId = r.Id
            });
        await _db.SaveChangesAsync();

        var status = await SigningStatusInternal(submission.Id);
        return Ok(new { submissionId = submission.Id, status });
    }

    [Authorize]
    [HttpGet("{slug}/signing/{submissionId:guid}/status")]
    public async Task<IActionResult> SigningStatus(string slug, Guid submissionId)
    {
        var sub = await _db.Submissions.FindAsync(submissionId);
        if (sub is null) return NotFound();
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Id == sub.FormId && f.Slug == slug);
        if (form is null) return NotFound();

        var status = await SigningStatusInternal(submissionId);
        return Ok(status);
    }

    [Authorize]
    [HttpPost("{slug}/signing/{submissionId:guid}/capture")]
    public async Task<IActionResult> CaptureSignature(string slug, Guid submissionId, [FromBody] CaptureSignatureReq req)
    {
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sub = await _db.Submissions.FindAsync(submissionId);
        if (sub is null) return NotFound();
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Id == sub.FormId && f.Slug == slug);
        if (form is null) return NotFound();

        var sigReq = await _db.SignatureRequirements.FirstOrDefaultAsync(r => r.Id == req.SignatureRequirementId && r.FormId == form.Id);
        if (sigReq is null) return BadRequest("Invalid signature requirement");

        var sigRow = await _db.SubmissionSignatures.FirstOrDefaultAsync(s => s.SubmissionId == submissionId && s.SignatureRequirementId == sigReq.Id);
        if (sigRow is null) return BadRequest("Signature row not created");
        if (sigRow.SignedAt is not null) return Conflict("Already signed");

        // Save PNG copy for audit
        var sigDir = Path.Combine(_env.ContentRootPath, "signatures");
        Directory.CreateDirectory(sigDir);
        var pngPath = Path.Combine(sigDir, $"sig_{submissionId:N}_{sigReq.Id:N}.png");
        try
        {
            var base64 = System.Text.RegularExpressions.Regex.Match(req.DataUrl, @"^data:image/\w+;base64,(.+)$").Groups[1].Value;
            await System.IO.File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(base64));
        }
        catch
        {
            // ignore audit save failure, stamping still attempted below via service
        }

        // Stamp into current PDF, produce a new file and update Submission
        var outPath = Path.Combine(Path.GetDirectoryName(sub.PdfPath)!, $"{Path.GetFileNameWithoutExtension(sub.PdfPath)}_sig_{sigReq.Id:N}.pdf");
        var stampedPath = _signature.StampSignaturePngIntoField(sub.PdfPath, outPath, sigReq.PdfFieldName, req.DataUrl);
        sub.PdfPath = stampedPath;

        // capture signer context
        var profile = await _db.UserProfiles.FindAsync(uid);
        sigRow.SignerUserId = uid;
        sigRow.SignerName = profile?.FullName ?? User.Identity?.Name;
        sigRow.SignerEmail = profile?.Email;
        sigRow.SignedAt = DateTime.UtcNow;
        sigRow.SignatureImagePath = System.IO.File.Exists(pngPath) ? pngPath : null;
        sigRow.SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _db.SaveChangesAsync();

        // If all required signatures complete, finalize: optionally flatten and email
        var allReq = await _db.SignatureRequirements.Where(r => r.FormId == form.Id && r.Required).ToListAsync();
        var signedReqIds = await _db.SubmissionSignatures.Where(s => s.SubmissionId == submissionId && s.SignedAt != null).Select(s => s.SignatureRequirementId).ToListAsync();
        var complete = allReq.All(r => signedReqIds.Contains(r.Id));

        string? emailMsgId = null;
        if (complete)
        {
            // read stored payload and email prefs
            var payload = System.Text.Json.JsonDocument.Parse(sub.PayloadJson);
            var values = payload.RootElement.GetProperty("Values").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            var emailObj = payload.RootElement.TryGetProperty("Email", out var e) ? e : default;
            var doFlatten = emailObj.ValueKind != System.Text.Json.JsonValueKind.Undefined && emailObj.TryGetProperty("Flatten", out var fl) ? (fl.GetBoolean()) : true;
            // Flatten the current PDF
            if (doFlatten)
            {
                var flattenPath = await _pdf.FillAsync(sub.PdfPath, new Dictionary<string, string>(), flatten: true, HttpContext.RequestAborted);
                sub.PdfPath = flattenPath;
            }

            // Email
            var tpl = form.EmailTemplate!;
            string Render(string s) => values.Aggregate(s, (acc, kv) => acc.Replace("{{" + kv.Key + "}}", kv.Value ?? ""));

            string to = tpl.To;
            string cc = tpl.Cc ?? string.Empty;
            string bcc = tpl.Bcc ?? string.Empty;
            if (emailObj.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            {
                if (emailObj.TryGetProperty("ToOverride", out var toOv) && !string.IsNullOrWhiteSpace(toOv.GetString())) to = toOv.GetString()!;
                if (emailObj.TryGetProperty("CcOverride", out var ccOv) && !string.IsNullOrWhiteSpace(ccOv.GetString())) cc = ccOv.GetString()!;
                if (emailObj.TryGetProperty("BccOverride", out var bccOv) && !string.IsNullOrWhiteSpace(bccOv.GetString())) bcc = bccOv.GetString()!;
            }

            var subject = Render(tpl.Subject);
            var body = Render(tpl.BodyHtml);

            emailMsgId = await _email.SendAsync(to, subject, body, sub.PdfPath);
            if (!string.IsNullOrWhiteSpace(cc)) await _email.SendAsync(cc, "(CC) " + subject, body, null);
            if (!string.IsNullOrWhiteSpace(bcc)) await _email.SendAsync(bcc, "(BCC) " + subject, body, null);

            sub.EmailMessageId = emailMsgId;
            await _db.SaveChangesAsync();
        }

        var status = await SigningStatusInternal(submissionId);
        return Ok(new { submissionId, complete, emailMessageId = emailMsgId, status });
    }

    private async Task<object> SigningStatusInternal(Guid submissionId)
    {
        var rows = await _db.SubmissionSignatures
            .Where(s => s.SubmissionId == submissionId)
            .Join(_db.SignatureRequirements, s => s.SignatureRequirementId, r => r.Id, (s, r) => new { s, r })
            .OrderBy(x => x.r.OrderIndex)
            .Select(x => new
            {
                x.r.Id,
                x.r.Name,
                x.r.PdfFieldName,
                x.r.SignerRole,
                x.r.OrderIndex,
                x.r.Required,
                SignedAt = x.s.SignedAt,
                SignerName = x.s.SignerName,
                SignerEmail = x.s.SignerEmail
            })
            .ToListAsync();
        return new { items = rows };
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
