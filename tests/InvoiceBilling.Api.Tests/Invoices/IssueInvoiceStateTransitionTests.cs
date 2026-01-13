using System.Net;
using System.Net.Http.Json;
using InvoiceBilling.Api.Dtos.Customers;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Products;
using InvoiceBilling.Api.Tests.Infrastructure;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBilling.Api.Tests.Invoices;

public sealed class IssueInvoiceStateTransitionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public IssueInvoiceStateTransitionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IssueInvoice_ShouldUpdateStatus_ToIssued()
    {
        var created = await CreateDraftInvoiceAsync();

        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        var payload = await issueResp.Content.ReadFromJsonAsync<IssueInvoiceApiResponse>();
        Assert.NotNull(payload);
        Assert.False(payload!.WasNoOp);
        Assert.True(payload.JobEnqueued); // Test host uses NoOp enqueuer; no LocalStack dependency.
        Assert.NotNull(payload.Invoice);
        Assert.Equal("Issued", payload.Invoice!.Status);

        // 5) Verify status persisted
        var after = await _client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}");
        Assert.NotNull(after);
        Assert.Equal("Issued", after!.Status);
    }

    [Fact]
    public async Task IssueInvoice_WhenAlreadyIssued_ShouldBeIdempotent_NoOpSuccess()
    {
        var created = await CreateDraftInvoiceAsync();

        // First issue
        var first = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second issue (retry)
        var second = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Assert response signals idempotent no-op
        var payload = await second.Content.ReadFromJsonAsync<IssueInvoiceApiResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.WasNoOp);
        Assert.True(payload.JobEnqueued);

        // Persisted state remains Issued
        var after = await _client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}");
        Assert.NotNull(after);
        Assert.Equal("Issued", after!.Status);
    }

    [Fact]
    public async Task IssueInvoice_WhenNonDraftStatus_ShouldReturnConflict()
    {
        var created = await CreateDraftInvoiceAsync();

        // Force a non-draft status directly in DB (no public API for this yet)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var invoice = await db.Invoices.SingleAsync(i => i.Id == created.Id);
            db.Entry(invoice).Property("Status").CurrentValue = InvoiceStatus.Paid;
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem!.Status);
        Assert.Contains("Only Draft invoices can be issued", problem.Detail ?? string.Empty);
    }

    [Fact]
    public async Task IssueInvoice_WhenInvoiceHasNoLines_ShouldReturnBadRequest()
    {
        var created = await CreateDraftInvoiceAsync();

        // Remove lines to simulate inconsistent draft data (validator should catch this)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var lines = await db.InvoiceLines.Where(l => l.InvoiceId == created.Id).ToListAsync();
            db.InvoiceLines.RemoveRange(lines);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Contains("Cannot issue an invoice without lines", problem.Detail ?? string.Empty);

        // Status remains Draft
        var after = await _client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}");
        Assert.NotNull(after);
        Assert.Equal("Draft", after!.Status);
    }

    private async Task<InvoiceDto> CreateDraftInvoiceAsync()
    {
        // 1) Create customer
        var customerResp = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            Name = $"Acme Customer {Guid.NewGuid():N}".Substring(0, 24)
        });
        Assert.Equal(HttpStatusCode.Created, customerResp.StatusCode);
        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);

        // 2) Create product
        var productResp = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Test Product",
            Sku = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
            UnitPrice = 100m,
            CurrencyCode = "INR"
        });

        Assert.Equal(HttpStatusCode.Created, productResp.StatusCode);

        var product = await productResp.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);

        // 3) Create draft invoice
        var today = DateTime.UtcNow.Date;
        var invoiceCreateResp = await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest
        {
            CustomerId = customer!.Id,
            IssueDate = today,
            DueDate = today.AddDays(7),
            CurrencyCode = "INR",
            Lines =
            {
                new CreateInvoiceLineRequest
                {
                    ProductId = product!.Id,
                    Description = "Line-1",
                    UnitPrice = 100m,
                    Quantity = 2m
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, invoiceCreateResp.StatusCode);

        var created = await invoiceCreateResp.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);
        return created;
    }

    private sealed record IssueInvoiceApiResponse(
        string? Message,
        Guid InvoiceId,
        bool JobEnqueued,
        string? JobEnqueueError,
        bool WasNoOp,
        InvoiceDto? Invoice
    );
}
