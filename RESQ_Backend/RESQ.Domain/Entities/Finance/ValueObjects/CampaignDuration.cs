using System;
using RESQ.Domain.Entities.Finance.Exceptions;

namespace RESQ.Domain.Entities.Finance.ValueObjects;

public record CampaignDuration
{
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    public CampaignDuration(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new InvalidCampaignDateException("Ngày bắt đầu không được lớn hơn ngày kết thúc.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public static CampaignDuration Create(DateOnly startDate, DateOnly endDate)
    {
        return new CampaignDuration(startDate, endDate);
    }

    public CampaignDuration Extend(DateOnly newEndDate)
    {
        if (newEndDate <= EndDate)
        {
            throw new InvalidCampaignDateException("Ngày kết thúc mới phải lớn hơn ngày kết thúc hiện tại.");
        }
        
        // Ensure new end date is not in the past relative to UTC Now
        if (newEndDate <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new InvalidCampaignDateException("Ngày kết thúc mới phải lớn hơn ngày hiện tại.");
        }

        return new CampaignDuration(StartDate, newEndDate);
    }

    public bool IsActive(DateOnly currentDate)
    {
        return currentDate >= StartDate && currentDate <= EndDate;
    }

    public bool HasEnded(DateOnly currentDate)
    {
        return currentDate > EndDate;
    }
    
    public bool HasStarted(DateOnly currentDate)
    {
        return currentDate >= StartDate;
    }
}