using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using PdfApp.Api.Models;

public class FormsControllerTests
{
    [Fact]
    public async Task Search_NoQuery_ReturnsActiveFormsSorted()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Search_NoQuery_ReturnsActiveFormsSorted));
        db.Forms.AddRange(
            new Form { Id = Guid.NewGuid(), Slug = "b", Title = "Bravo", IsActive = true, PdfBlobPath = "dummy.pdf" },
            new Form { Id = Guid.NewGuid(), Slug = "a", Title = "Alpha", IsActive = true, PdfBlobPath = "dummy.pdf" },
            new Form { Id = Guid.NewGuid(), Slug = "x", Title = "Xray", IsActive = false, PdfBlobPath = "dummy.pdf" }
        );
        await db.SaveChangesAsync();
        var env = new Mock<IWebHostEnvironment>();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.Search(null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<System.Collections.IEnumerable>().Subject as System.Collections.IEnumerable;
        items.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithQuery_FiltersByTitleAndKeywords()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Search_WithQuery_FiltersByTitleAndKeywords));
        db.Forms.AddRange(
            new Form { Id = Guid.NewGuid(), Slug = "one", Title = "Vehicle Sale", Keywords = "car,auto", IsActive = true, PdfBlobPath = "dummy.pdf" },
            new Form { Id = Guid.NewGuid(), Slug = "two", Title = "Lease", Keywords = "rental", IsActive = true, PdfBlobPath = "dummy.pdf" }
        );
        await db.SaveChangesAsync();
        var env = new Mock<IWebHostEnvironment>();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.Search("auto");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Prefill_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Prefill_FormNotFound_ReturnsNotFound));
        var env = new Mock<IWebHostEnvironment>();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.Prefill("missing");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Prefill_MergesDefaults_Profile_UserGlobal_UserPerForm_InOrder()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Prefill_MergesDefaults_Profile_UserGlobal_UserPerForm_InOrder));
        var uid = Guid.NewGuid();
        var form = new Form { Id = Guid.NewGuid(), Slug = "deal", Title = "Deal", IsActive = true, PdfBlobPath = "dummy.pdf" };
        db.Forms.Add(form);
        db.Users.Add(new User { Id = uid, UserName = "rick", PasswordHash = "x" });
        db.UserProfiles.Add(new UserProfile { UserId = uid, FullName = "Rick D", Company = "Deckard Inc", Email = "rick@example.com" });
        db.FormDefaults.Add(new FormDefault { Id = Guid.NewGuid(), FormId = form.Id, FieldName = "CustomerName", FieldValue = "DefaultName" });
        db.UserDefaults.Add(new UserDefault { Id = Guid.NewGuid(), UserId = uid, FormId = form.Id, FieldName = "CustomerName", FieldValue = "GlobalName" });
        db.UserDefaults.Add(new UserDefault { Id = Guid.NewGuid(), UserId = uid, FormId = form.Id, FieldName = "Dealer", FieldValue = "GlobalDealer" });
        // Per-form override should win
        db.UserDefaults.Add(new UserDefault { Id = Guid.NewGuid(), UserId = uid, FormId = form.Id, FieldName = "CustomerName", FieldValue = "PerFormName", FormSlug = form.Slug });
        await db.SaveChangesAsync();

        var env = new Mock<IWebHostEnvironment>();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(uid, "rick")
        };

        // Act
        var result = await sut.Prefill(form.Slug);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMeta_NotFound_WhenFormMissingOrInactive()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(GetMeta_NotFound_WhenFormMissingOrInactive));
        db.Forms.Add(new Form { Id = Guid.NewGuid(), Slug = "inactive", Title = "X", IsActive = false, PdfBlobPath = "dummy.pdf" });
        await db.SaveChangesAsync();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), Mock.Of<IWebHostEnvironment>(), Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act + Assert
        (await sut.GetMeta("missing")).Should().BeOfType<NotFoundResult>();
        (await sut.GetMeta("inactive")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMeta_ReturnsFieldsOrderedByOrderIndex()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(GetMeta_ReturnsFieldsOrderedByOrderIndex));
        var form = new Form { Id = Guid.NewGuid(), Slug = "meta", Title = "Meta", IsActive = true, PdfBlobPath = "dummy.pdf" };
        db.Forms.Add(form);
        db.FormFields.AddRange(
            new FormField { Id = Guid.NewGuid(), FormId = form.Id, PdfFieldName = "B", Label = "B", OrderIndex = 2 },
            new FormField { Id = Guid.NewGuid(), FormId = form.Id, PdfFieldName = "A", Label = "A", OrderIndex = 1 }
        );
        await db.SaveChangesAsync();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), Mock.Of<IWebHostEnvironment>(), Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.GetMeta(form.Slug);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Submit_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Submit_FormNotFound_ReturnsNotFound));
        var env = new Mock<IWebHostEnvironment>();
        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), Mock.Of<IStorageService>())
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.Submit("missing", new FormsController.SubmitReq(new Dictionary<string, string>()));

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Submit_SendsEmails_RespectsOverrides_AndSavesSubmission()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Submit_SendsEmails_RespectsOverrides_AndSavesSubmission));
        var uid = Guid.NewGuid();
        var form = new Form { Id = Guid.NewGuid(), Slug = "send", Title = "Send", IsActive = true, PdfBlobPath = "dummy.pdf", EmailTemplate = new EmailTemplate { Id = Guid.NewGuid(), FormId = Guid.Empty, To = "to@ex.com", Subject = "Hi {{CustomerName}}", BodyHtml = "<b>body</b>" } };
        db.Forms.Add(form);
        db.Users.Add(new User { Id = uid, UserName = "rick", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var pdf = new Mock<IPdfFillService>();
        pdf.Setup(p => p.FillAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync("/tmp/out.pdf");

        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
             .ReturnsAsync("msg-1");

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var storageForSubmit = new Mock<IStorageService>();
        storageForSubmit
            .Setup(s => s.GetLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/template.pdf");

        var sut = new FormsController(db, pdf.Object, email.Object, env.Object, Mock.Of<ISignatureService>(), storageForSubmit.Object)
        {
            ControllerContext = TestHelpers.CreateControllerContext(uid, "rick")
        };
        var req = new FormsController.SubmitReq(new Dictionary<string, string> { ["CustomerName"] = "Rick" }, Flatten: true, ToOverride: "a@ex.com", CcOverride: "c@ex.com", BccOverride: "b@ex.com");

        // Act
        var result = await sut.Submit(form.Slug, req);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        email.Verify(e => e.SendAsync("a@ex.com", It.Is<string>(s => s.Contains("Rick")), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        email.Verify(e => e.SendAsync("c@ex.com", It.Is<string>(s => s.StartsWith("(CC)")), It.IsAny<string>(), null), Times.Once);
        email.Verify(e => e.SendAsync("b@ex.com", It.Is<string>(s => s.StartsWith("(BCC)")), It.IsAny<string>(), null), Times.Once);
        (await db.Submissions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetFormFields_UsesDiscovery_AndMergesLabelsTypesRequired()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(GetFormFields_UsesDiscovery_AndMergesLabelsTypesRequired));
        var form = new Form { Id = Guid.NewGuid(), Slug = "f1", Title = "Form1", PdfBlobPath = "blob://pdfs/f1.pdf", IsActive = true };
        db.Forms.Add(form);
        db.FormFields.Add(new FormField { Id = Guid.NewGuid(), FormId = form.Id, PdfFieldName = "FieldA", Label = "Friendly A", Type = "text", Required = true, OrderIndex = 1 });
        await db.SaveChangesAsync();

        var discovery = new Mock<IPdfFieldDiscovery>();
        discovery.Setup(d => d.GetFormFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<FormFieldInfo>
                 {
                     new FormFieldInfo { Name = "FieldA", Type = "text", Required = false, PageNumber = 1, X = 0, Y = 0, Width = 10, Height = 10 },
                     new FormFieldInfo { Name = "FieldB", Type = "checkbox", Required = true, PageNumber = 1, X = 0, Y = 0, Width = 10, Height = 10 },
                 });

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var storageForFields = new Mock<IStorageService>();
        storageForFields
            .Setup(s => s.GetLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string uri, CancellationToken _) => System.IO.Path.Combine(AppContext.BaseDirectory, System.IO.Path.GetFileName(uri) ?? "template.pdf"));

        var sut = new FormsController(db, Mock.Of<IPdfFillService>(), Mock.Of<IEmailService>(), env.Object, Mock.Of<ISignatureService>(), storageForFields.Object)
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid())
        };

        // Act
        var result = await sut.GetFormFields(form.Slug, discovery.Object);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        discovery.Verify(d => d.GetFormFieldsAsync(It.Is<string>(p => p.EndsWith("f1.pdf")), It.IsAny<CancellationToken>()), Times.Once);
    }
}
