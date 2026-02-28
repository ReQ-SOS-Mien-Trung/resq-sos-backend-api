using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/donations")]
[ApiController]
public class DonationController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDonationRequestDto dto)
    {
        var command = new CreateDonationCommand
        {
            FundCampaignId = dto.FundCampaignId,
            DonorName = dto.DonorName,
            DonorEmail = dto.DonorEmail,
            Amount = dto.Amount,
            Note = dto.Note
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("payment-return")]
    public async Task<IActionResult> ProcessPaymentReturn([FromBody] WebhookType webhook)
    {
        var command = new ProcessPaymentReturnCommand { WebhookData = webhook };
        var success = await _mediator.Send(command);

        if (success)
        {
            return Ok(new { message = "Payment status updated successfully." });
        }

        return BadRequest(new { message = "Failed to update payment status." });

        //return Ok();
    }
}
