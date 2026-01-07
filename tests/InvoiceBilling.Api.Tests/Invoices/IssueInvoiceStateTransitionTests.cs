using System.Net;
using System.Net.Http.Json;
using InvoiceBilling.Api.Dtos.Customers;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Products;
using InvoiceBilling.Api.Tests.Infrastructure;

namespace InvoiceBilling.Api.Tests.Invoices;

public sealed class IssueInvoiceStateTransitionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IssueInvoiceStateTransitionTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IssueInvoice_ShouldUpdateStatus_ToIssued()
    {
        // 1) Create customer
        var customerResp = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            Name = "Acme Customer"
        });

        Assert.Equal(HttpStatusCode.Created, customerResp.StatusCode);

        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);
        Assert.NotEqual(Guid.Empty, customer!.Id);

        // 2) Create product
        var productResp = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Test Product",
            Sku = "SKU-001",
            UnitPrice = 100m,
            CurrencyCode = "INR"
        });

        Assert.Equal(HttpStatusCode.Created, productResp.StatusCode);

        var product = await productResp.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        Assert.NotEqual(Guid.Empty, product!.Id);

        // 3) Create draft invoice
        var today = DateTime.UtcNow.Date;

        var invoiceCreateResp = await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest
        {
            CustomerId = customer.Id,
            IssueDate = today,
            DueDate = today.AddDays(7),
            CurrencyCode = "INR",
            Lines =
            {
                new CreateInvoiceLineRequest
                {
                    ProductId = product.Id,
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

        // 4) Issue invoice
        var issueResp = await _client.PostAsync($"/api/invoices/{created.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        // 5) Verify status persisted
        var after = await _client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}");
        Assert.NotNull(after);
        Assert.Equal("Issued", after!.Status);
    }
}
