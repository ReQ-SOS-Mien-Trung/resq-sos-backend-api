using System.Text.Json;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Services;

public interface ISosPriorityEvaluationService
{
    SosRuleEvaluationModel Evaluate(int sosRequestId, string? structuredDataJson, string? sosType);
}

public class SosPriorityEvaluationService : ISosPriorityEvaluationService
{
    private const string RULE_VERSION = "1.0";

    public SosRuleEvaluationModel Evaluate(int sosRequestId, string? structuredDataJson, string? sosType)
    {
        var evaluation = new SosRuleEvaluationModel
        {
            SosRequestId = sosRequestId,
            RuleVersion = RULE_VERSION,
            CreatedAt = DateTime.UtcNow
        };

        StructuredData? structuredData = null;
        if (!string.IsNullOrWhiteSpace(structuredDataJson))
        {
            try
            {
                structuredData = JsonSerializer.Deserialize<StructuredData>(structuredDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // Invalid JSON, use default scores
            }
        }

        // Calculate scores based on structured data
        evaluation.MedicalScore = CalculateMedicalScore(structuredData);
        evaluation.InjuryScore = CalculateInjuryScore(structuredData);
        evaluation.MobilityScore = CalculateMobilityScore(structuredData);
        evaluation.EnvironmentScore = CalculateEnvironmentScore(structuredData, sosType);
        evaluation.FoodScore = CalculateFoodScore(structuredData);

        // Calculate total score (weighted average)
        evaluation.TotalScore = CalculateTotalScore(evaluation);

        // Determine priority level based on total score
        evaluation.PriorityLevel = DeterminePriorityLevel(evaluation.TotalScore).ToString();

        // Determine items needed based on situation
        evaluation.ItemsNeeded = DetermineItemsNeeded(structuredData, sosType);

        return evaluation;
    }

    private static double CalculateMedicalScore(StructuredData? data)
    {
        if (data == null) return 0;

        double score = 0;

        // Need medical assistance
        if (data.NeedMedical == true) score += 30;

        // Has medical issues
        if (data.MedicalIssues != null && data.MedicalIssues.Count > 0)
        {
            score += Math.Min(data.MedicalIssues.Count * 10, 30);

            // Specific critical conditions
            if (data.MedicalIssues.Contains("BLEEDING")) score += 10;
            if (data.MedicalIssues.Contains("UNCONSCIOUS")) score += 20;
            if (data.MedicalIssues.Contains("BREATHING_DIFFICULTY")) score += 15;
            if (data.MedicalIssues.Contains("CARDIAC")) score += 20;
        }

        return Math.Min(score, 100);
    }

    private static double CalculateInjuryScore(StructuredData? data)
    {
        if (data == null) return 0;

        double score = 0;

        if (data.HasInjured == true) score += 40;
        if (data.OthersAreStable == false) score += 30;

        return Math.Min(score, 100);
    }

    private static double CalculateMobilityScore(StructuredData? data)
    {
        if (data == null) return 0;

        // Cannot move = higher urgency
        if (data.CanMove == false) return 80;
        if (data.CanMove == true) return 20;

        return 50; // Unknown
    }

    private static double CalculateEnvironmentScore(StructuredData? data, string? sosType)
    {
        double score = 0;

        // SOS type severity
        if (!string.IsNullOrEmpty(sosType))
        {
            score += sosType.ToUpper() switch
            {
                "RESCUE" => 40,
                "MEDICAL" => 50,
                "EVACUATION" => 35,
                "SUPPLY" => 20,
                _ => 25
            };
        }

        // Situation severity
        if (data?.Situation != null)
        {
            score += data.Situation.ToUpper() switch
            {
                "FLOODING" => 40,
                "FIRE" => 50,
                "EARTHQUAKE" => 45,
                "LANDSLIDE" => 40,
                "STORM" => 30,
                "MEDICAL_EMERGENCY" => 45,
                "ACCIDENT" => 35,
                _ => 20
            };
        }

        return Math.Min(score, 100);
    }

    private static double CalculateFoodScore(StructuredData? data)
    {
        if (data == null) return 0;

        double score = 0;

        // People count affects food/supply needs
        if (data.PeopleCount != null)
        {
            var totalPeople = (data.PeopleCount.Adult ?? 0) +
                              (data.PeopleCount.Child ?? 0) +
                              (data.PeopleCount.Elderly ?? 0);

            score += Math.Min(totalPeople * 10, 50);

            // Vulnerable groups need more attention
            if (data.PeopleCount.Child > 0) score += 15;
            if (data.PeopleCount.Elderly > 0) score += 15;
        }

        return Math.Min(score, 100);
    }

    private static double CalculateTotalScore(SosRuleEvaluationModel evaluation)
    {
        // Weighted average
        const double medicalWeight = 0.30;
        const double injuryWeight = 0.25;
        const double mobilityWeight = 0.15;
        const double environmentWeight = 0.20;
        const double foodWeight = 0.10;

        return (evaluation.MedicalScore * medicalWeight) +
               (evaluation.InjuryScore * injuryWeight) +
               (evaluation.MobilityScore * mobilityWeight) +
               (evaluation.EnvironmentScore * environmentWeight) +
               (evaluation.FoodScore * foodWeight);
    }

    private static SosPriorityLevel DeterminePriorityLevel(double totalScore)
    {
        return totalScore switch
        {
            >= 70 => SosPriorityLevel.Critical,
            >= 50 => SosPriorityLevel.High,
            >= 30 => SosPriorityLevel.Medium,
            _ => SosPriorityLevel.Low
        };
    }

    private static string? DetermineItemsNeeded(StructuredData? data, string? sosType)
    {
        var items = new List<string>();

        if (data?.NeedMedical == true || data?.HasInjured == true)
        {
            items.Add("FIRST_AID_KIT");
            items.Add("MEDICAL_SUPPLIES");
        }

        if (data?.MedicalIssues?.Contains("BLEEDING") == true)
        {
            items.Add("BANDAGES");
            items.Add("BLOOD_CLOTTING_AGENTS");
        }

        if (data?.Situation?.ToUpper() == "FLOODING")
        {
            items.Add("LIFE_JACKET");
            items.Add("RESCUE_BOAT");
            items.Add("ROPE");
        }

        if (data?.Situation?.ToUpper() == "FIRE")
        {
            items.Add("FIRE_EXTINGUISHER");
            items.Add("PROTECTIVE_GEAR");
        }

        if (data?.PeopleCount != null)
        {
            var totalPeople = (data.PeopleCount.Adult ?? 0) +
                              (data.PeopleCount.Child ?? 0) +
                              (data.PeopleCount.Elderly ?? 0);
            if (totalPeople > 0)
            {
                items.Add("FOOD_RATIONS");
                items.Add("WATER");
                items.Add("BLANKETS");
            }
        }

        if (sosType?.ToUpper() == "EVACUATION")
        {
            items.Add("TRANSPORT_VEHICLE");
            items.Add("STRETCHER");
        }

        return items.Count > 0 ? JsonSerializer.Serialize(items) : null;
    }

    // Internal class for deserializing structured data
    private class StructuredData
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public List<string>? MedicalIssues { get; set; }
        public bool? OthersAreStable { get; set; }
        public PeopleCount? PeopleCount { get; set; }
        public bool? CanMove { get; set; }
        public bool? NeedMedical { get; set; }
        public string? AdditionalDescription { get; set; }
    }

    private class PeopleCount
    {
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public int? Elderly { get; set; }
    }
}
