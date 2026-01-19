using System.Net;
using System.Net.Http.Json;
using InvoiceBilling.Api.Dtos.Customers;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Payments;
using InvoiceBilling.Api.Dtos.Products;
using InvoiceBilling.Api.Tests.Infrastructure;

namespace InvoiceBilling.Api.Tests.Invoices;

public sealed class InvoiceStatusPollingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InvoiceStatusPollingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_ShouldReturnDraftAndNotIssued_ForDraftInvoice()
    {
        var created = await CreateDraftInvoiceAsync();

        var resp = await _client.GetAsync($"/api/invoices/{created.Id}/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<InvoiceStatusDto>();
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto!.Id);
        Assert.Equal("Draft", dto.Status);
        Assert.Equal("NotIssued", dto.PdfStatus);
        Assert.Null(dto.PdfDownloadUrl);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnIssuedAndPending_WhenPdfNotAttachedYet()
    {
        var created = await CreateDraftInvoiceAsync();

        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        var resp = await _client.GetAsync($"/api/invoices/{created.Id}/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<InvoiceStatusDto>();
        Assert.NotNull(dto);
        Assert.Equal("Issued", dto!.Status);
        Assert.Equal("Pending", dto.PdfStatus);
        Assert.Null(dto.PdfDownloadUrl);
    }



    [Fact]
    public async Task GetStatus_ShouldReturnPaidAndPending_WhenInvoicePaidButPdfNotAttachedYet()
    {
        var created = await CreateDraftInvoiceAsync();

        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        var payResp = await _client.PostAsJsonAsync($"/api/invoices/{created.Id}/payments", new RecordPaymentRequest
        {
            Amount = created.GrandTotal,
            PaidAtUtc = DateTime.UtcNow,
            Method = "Cash"
        });
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var resp = await _client.GetAsync($"/api/invoices/{created.Id}/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<InvoiceStatusDto>();
        Assert.NotNull(dto);
        Assert.Equal("Paid", dto!.Status);
        Assert.Equal("Pending", dto.PdfStatus);
        Assert.Equal(0m, dto.BalanceDue);
        Assert.True(dto.PaidTotal > 0m);
    }
    private async Task<InvoiceDto> CreateDraftInvoiceAsync()
    {
        // 1) Create customer
        var customerResp = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            Name = $"Polling Customer {Guid.NewGuid():N}".Substring(0, 24)
        });
        Assert.Equal(HttpStatusCode.Created, customerResp.StatusCode);
        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);

        // 2) Create product
        var productResp = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Polling Product",
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
