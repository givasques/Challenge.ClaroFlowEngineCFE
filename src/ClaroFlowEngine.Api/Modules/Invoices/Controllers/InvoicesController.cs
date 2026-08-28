using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Invoices.Dtos;
using ClaroFlowEngine.Api.Modules.Invoices.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Invoices.Controllers;

/// <summary>Faturas do cliente, usadas pela intenção "contestação de cobrança indevida" (ETAPA 2, Passo C).</summary>
[ApiController]
[Route("invoices")]
[Produces("application/json")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;

    public InvoicesController(IInvoiceService service) => _service = service;

    /// <summary>Retorna as últimas faturas do cliente, mais recente primeiro.</summary>
    /// <response code="200">Faturas encontradas (lista pode ser vazia).</response>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(InvoiceListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCustomer(
        Guid customerId, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        var result = await _service.GetByCustomerAsync(customerId, limit <= 0 ? 3 : limit, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retorna o detalhe de uma fatura, incluindo os itens de linha.</summary>
    /// <response code="200">Fatura encontrada.</response>
    /// <response code="404">Fatura não encontrada.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }
}
