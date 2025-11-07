using System.Security.Claims;
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

    public FormsController(AppDb db, IPdfFillService pdf, IEmailService email, IWebHostEnvironment env, ISignatureService signature)
      => (_db, _pdf, _email, _env, _signature) = (db, pdf, email, env, signature);

    [Authorize, HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var query = _db.Forms.Where(f => f.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(f => (f.Title + " " + (f.Keywords ?? "")).Contains(q));
        var list = await query.OrderBy(f => f.Title).Select(f => new { f.Slug, f.Title }).ToListAsync();
        return Ok(list);
    }

    [Authorize, HttpGet("{slug}/prefill")]
    public async Task<IActionResult> Prefill(string slug)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var prefill = await BuildPrefillAsync(uid, form);
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
        var path = ResolvePdfPath(form.PdfBlobPath);
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }

    public sealed record PreviewReq(Dictionary<string, string> Values);

    [Authorize]
    [HttpPost("{slug}/preview")]
    public async Task<IActionResult> PreviewFilled(string slug, [FromBody] PreviewReq req)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var values = await BuildPrefillAsync(uid, form);
        foreach (var kv in req.Values) values[kv.Key] = kv.Value; // user overrides

        // Create a filled temp file
        var path = await _pdf.FillAsync(ResolvePdfPath(form.PdfBlobPath), values, flatten: false, HttpContext.RequestAborted);
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        System.IO.File.Delete(path); // temp
        return File(bytes, "application/pdf", $"preview_{slug}.pdf");
    }
    public sealed record SubmitReq(
        Dictionary<string, string> Values,
        bool Flatten = false,
        string? ToOverride = null,
        string? CcOverride = null,
        string? BccOverride = null
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

        var values = await BuildPrefillAsync(uid, form);
        foreach (var kv in req.Values) values[kv.Key] = kv.Value;

        // (Optional) validate requireds from FormFields here

        var pdfPath = await _pdf.FillAsync(ResolvePdfPath(form.PdfBlobPath), values, req.Flatten, HttpContext.RequestAborted);

        var tpl = form.EmailTemplate!;
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

    private async Task<Dictionary<string, string>> BuildPrefillAsync(Guid userId, Form form)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Form defaults
        var fd = await _db.FormDefaults.Where(x => x.FormId == form.Id).ToDictionaryAsync(x => x.FieldName, x => x.FieldValue);
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

        //var signedPath = Path.Combine(outDir, "filled_signed.pdf");
        //_signature.StampSignaturePngIntoField(pdfPath, signedPath, "CustomerSignature", req.CustomerSignatureDataUrl);

        return result;
    }

    private string ResolvePdfPath(string blob)
    {
        // Example: map "blob://pdfs/foo.pdf" to a local path
        if (blob.StartsWith("blob://pdfs/"))
        {
            var local = Path.Combine(_env.ContentRootPath, "wwwroot", "pdfs", blob.Replace("blob://pdfs/", ""));
            return local;
        }
        return blob;
    }

    [Authorize]
    [HttpGet("{slug}/fields")]
    public async Task<IActionResult> GetFormFields(string slug, [FromServices] IPdfFieldDiscovery discovery)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);
        if (form is null) return NotFound();

        var path = ResolvePdfPath(form.PdfBlobPath);
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
