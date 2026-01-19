using System.Net;
using System.Net.Http.Json;
using InvoiceBilling.Api.Dtos.Customers;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Payments;
using InvoiceBilling.Api.Dtos.Products;
using InvoiceBilling.Api.Tests.Infrastructure;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBilling.Api.Tests.Invoices;

public sealed class InvoicePaymentsAndOverdueTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvoicePaymentsAndOverdueTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecordPayment_ShouldReturn409_ForDraftInvoice()
    {
        var created = await CreateDraftInvoiceAsync();

        var resp = await _client.PostAsJsonAsync($"/api/invoices/{created.Id}/payments", new RecordPaymentRequest
        {
            Amount = 10m,
            PaidAtUtc = DateTime.UtcNow,
            Method = "Cash"
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task RecordPayment_ShouldUpdateTotals_AndTransitionToPaid_WhenFullyPaid()
    {
        var created = await CreateDraftInvoiceAsync();

        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        var payResp = await _client.PostAsJsonAsync($"/api/invoices/{created.Id}/payments", new RecordPaymentRequest
        {
            Amount = created.GrandTotal,
            PaidAtUtc = DateTime.UtcNow,
            Method = "Cash",
            Reference = "TXN-PAID-001"
        });

        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var invoice = await _client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}");
        Assert.NotNull(invoice);

        Assert.Equal("Paid", invoice!.Status);
        Assert.Equal(created.GrandTotal, invoice.PaidTotal);
        Assert.Equal(0m, invoice.BalanceDue);

        // Paying again should be rejected as a state conflict.
        var payAgain = await _client.PostAsJsonAsync($"/api/invoices/{created.Id}/payments", new RecordPaymentRequest
        {
            Amount = 1m,
            PaidAtUtc = DateTime.UtcNow,
            Method = "Cash"
        });

        Assert.Equal(HttpStatusCode.Conflict, payAgain.StatusCode);
    }

    [Fact]
    public async Task RecordPayment_ShouldReturn400_WhenOverpaying()
    {
        var created = await CreateDraftInvoiceAsync();

        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        var overpayResp = await _client.PostAsJsonAsync($"/api/invoices/{created.Id}/payments", new RecordPaymentRequest
        {
            Amount = created.GrandTotal + 0.01m,
            PaidAtUtc = DateTime.UtcNow,
            Method = "Cash"
        });

        Assert.Equal(HttpStatusCode.BadRequest, overpayResp.StatusCode);
    }

    [Fact]
    public async Task Overdue_ShouldBeDerived_InStatusEndpoint_AndListFiltering()
    {
        var (customer, product) = await CreateCustomerAndProductAsync();
        var overdueInvoiceId = await SeedOverdueInvoiceAsync(customer, product);

        var status = await _client.GetFromJsonAsync<InvoiceStatusDto>($"/api/invoices/{overdueInvoiceId}/status");
        Assert.NotNull(status);
        Assert.Equal("Overdue", status!.Status);
        Assert.True(status.BalanceDue > 0m);
        Assert.Equal("Pending", status.PdfStatus);

        var overdueList = await _client.GetFromJsonAsync<List<InvoiceDto>>($"/api/invoices?status=Overdue&page=1&pageSize=50");
        Assert.NotNull(overdueList);
        Assert.Contains(overdueList!, i => i.Id == overdueInvoiceId && i.Status == "Overdue");

        var issuedList = await _client.GetFromJsonAsync<List<InvoiceDto>>($"/api/invoices?status=Issued&page=1&pageSize=50");
        Assert.NotNull(issuedList);
        Assert.DoesNotContain(issuedList!, i => i.Id == overdueInvoiceId);
    }

    private async Task<(CustomerDto Customer, ProductDto Product)> CreateCustomerAndProductAsync()
    {
        var customerResp = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            Name = $"Overdue Customer {Guid.NewGuid():N}".Substring(0, 24)
        });
        Assert.Equal(HttpStatusCode.Created, customerResp.StatusCode);
        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);

        var productResp = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Overdue Product",
            Sku = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
            UnitPrice = 100m,
            CurrencyCode = "INR"
        });
        Assert.Equal(HttpStatusCode.Created, productResp.StatusCode);
        var product = await productResp.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);

        return (customer!, product!);
    }

    private async Task<Guid> SeedOverdueInvoiceAsync(CustomerDto customer, ProductDto product)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

        var today = DateTime.UtcNow.Date;
        var issueDate = today.AddDays(-10);
        var dueDate = today.AddDays(-1);

        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: $"INV-OD-{Guid.NewGuid():N}".Substring(0, 20),
            customerId: customer.Id,
            issueDate: issueDate,
            dueDate: dueDate,
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (product.Id, "L1", 100m, 1m) });

        invoice.Issue(issueDate);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return invoice.Id;
    }

    private async Task<InvoiceDto> CreateDraftInvoiceAsync()
    {
        // 1) Create customer
        var customerResp = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            Name = $"Payments Customer {Guid.NewGuid():N}".Substring(0, 24)
        });
        Assert.Equal(HttpStatusCode.Created, customerResp.StatusCode);
        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);

        // 2) Create product
        var productResp = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Payments Product",
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
                    Quantity = 1m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, invoiceCreateResp.StatusCode);

        var created = await invoiceCreateResp.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);

        return created;
    }
}
