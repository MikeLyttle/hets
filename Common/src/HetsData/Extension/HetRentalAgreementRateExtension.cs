﻿using System.ComponentModel.DataAnnotations.Schema;

namespace HetsData.Model
{
    public partial class HetRentalAgreementRate
    {
        [NotMapped]
        public string RatePeriod { get; set; }        
    }
}
