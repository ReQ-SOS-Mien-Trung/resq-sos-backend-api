using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation
{
    public class CreateDonationResponse
    {
        public int DonationId { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
    }
}
