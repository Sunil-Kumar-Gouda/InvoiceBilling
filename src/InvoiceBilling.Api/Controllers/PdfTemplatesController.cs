using InvoiceBilling.Application.PdfTemplates.DeleteActivePdfTemplate;
using InvoiceBilling.Application.PdfTemplates.GetActivePdfTemplate;
using InvoiceBilling.Application.PdfTemplates.PreviewInvoicePdf;
using InvoiceBilling.Application.PdfTemplates.SaveActivePdfTemplate;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/pdf-templates")]
//[Authorize(Roles = "Admin,TemplateDesigner")]
//[Authorize(Roles = "Admin")]
public sealed class PdfTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PdfTemplatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the active template JSON (exactly as saved by the UI).
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetActivePdfTemplateQuery(), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return Content(result.TemplateJson!, "application/json");
    }

    /// <summary>
    /// Upserts the active template JSON.
    /// </summary>
    [HttpPut("active")]
    public async Task<IActionResult> PutActive([FromBody] JsonElement template, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SaveActivePdfTemplateCommand(TemplateJson: template.GetRawText()),
            ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    /// <summary>
    /// Deletes the active template.
    /// </summary>
    [HttpDelete("active")]
    public async Task<IActionResult> DeleteActive(CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteActivePdfTemplateCommand(), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    /// <summary>
    /// Generates a PDF preview for a given invoice using the provided template JSON.
    /// (Does NOT persist the template.)
    /// </summary>
    [HttpPost("preview/{invoiceId:guid}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview(Guid invoiceId, [FromBody] JsonElement template, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new PreviewInvoicePdfQuery(
                InvoiceId: invoiceId,
                TemplateJson: template.GetRawText()),
            ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return File(result.PdfBytes!, "application/pdf", $"invoice-{invoiceId}-preview.pdf");
    }
}
