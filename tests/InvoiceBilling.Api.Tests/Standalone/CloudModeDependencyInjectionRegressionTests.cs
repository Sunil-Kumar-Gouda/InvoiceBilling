using InvoiceBilling.Api.Tests.Infrastructure;
using InvoiceBilling.Application.Common.Jobs;
using InvoiceBilling.Application.Common.Storage;
using InvoiceBilling.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBilling.Api.Tests.Standalone;

/// <summary>
/// Regression tests: the existing <see cref="TestWebApplicationFactory"/> uses cloud mode
/// (the default). Verify that cloud-mode registrations still resolve correctly after
/// the standalone mode changes.
/// </summary>
public sealed class CloudModeDependencyInjectionRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CloudModeDependencyInjectionRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Cloud_mode_resolves_S3InvoicePdfStorage_for_IInvoicePdfStorage()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IInvoicePdfStorage>();

        Assert.IsType<S3InvoicePdfStorage>(storage);
    }

    [Fact]
    public void Cloud_mode_resolves_NoOp_enqueuer_because_test_factory_overrides_it()
    {
        // TestWebApplicationFactory replaces the real SQS enqueuer with a no-op.
        // This test verifies the override still works after DependencyInjection.cs changes.
        using var scope = _factory.Services.CreateScope();
        var enqueuer = scope.ServiceProvider.GetRequiredService<IInvoicePdfJobEnqueuer>();

        Assert.IsType<NoOpInvoicePdfJobEnqueuer>(enqueuer);
    }

    [Fact]
    public void Cloud_mode_resolves_AWS_S3_client()
    {
        var s3 = _factory.Services.GetService(typeof(Amazon.S3.IAmazonS3));
        Assert.NotNull(s3);
    }

    [Fact]
    public void Cloud_mode_resolves_AWS_SQS_client()
    {
        var sqs = _factory.Services.GetService(typeof(Amazon.SQS.IAmazonSQS));
        Assert.NotNull(sqs);
    }
}
