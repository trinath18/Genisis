using System.Globalization;

namespace Genisis.Api.Registration;

/// <summary>Pure ports of the legacy registration helpers (sequence, check digits, age, proration).</summary>
public static class LegacyRules
{
    /// <summary>Next payor sequence after <paramref name="current"/> (A00001..A99999, then B00001...); null starts at A00001.</summary>
    public static string NextPayorSequence(string? current)
    {
        current = current?.Trim();
        if (string.IsNullOrEmpty(current)) return "A00001";
        var letter = current[0];
        var number = int.Parse(current[1..], CultureInfo.InvariantCulture);
        if (number != 99999) number++;
        else
        {
            letter = (char)(letter + 1);
            number = 1;
        }
        return letter + number.ToString("00000", CultureInfo.InvariantCulture);
    }

    /// <summary>Check digits cycle 01..99.</summary>
    public static string NextCheckDigits(string current)
    {
        current = current.Trim();
        return current == "99" ? "01" : (int.Parse(current, CultureInfo.InvariantCulture) + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Card security code cycles 1001..9999.</summary>
    public static string NextSecurityCode(string current)
    {
        current = current.Trim();
        return current == "9999" ? "1001" : (int.Parse(current, CultureInfo.InvariantCulture) + 1).ToString("0000", CultureInfo.InvariantCulture);
    }

    /// <summary>Age at <paramref name="onDate"/> in whole years; under one year counts as 0 days ("D" band).</summary>
    public static (int Value, bool InDays) Age(DateTime onDate, DateTime dateOfBirth)
    {
        if (onDate.Year - dateOfBirth.Year <= 0) return (-1, true);
        var years = onDate.Year - dateOfBirth.Year;
        if (years <= 17 && (dateOfBirth.Month > onDate.Month || (dateOfBirth.Month == onDate.Month && dateOfBirth.Day > onDate.Day)))
            years--;
        return (years, false);
    }

    public record AgeBand(string AgeCode, string? Description, string AgeFrom, string AgeTo, string? PlanCode);

    /// <summary>Selects the AGE row for an age: plan-specific bands first, then bands without a plan.</summary>
    public static AgeBand? PickAgeBand(IEnumerable<AgeBand> bands, (int Value, bool InDays) age, string? planCode)
    {
        var list = bands.ToList();
        var plan = planCode?.Trim();
        return Pick(list.Where(b => !string.IsNullOrEmpty(plan) && b.PlanCode?.Trim() == plan))
            ?? Pick(list.Where(b => string.IsNullOrWhiteSpace(b.PlanCode)));

        AgeBand? Pick(IEnumerable<AgeBand> candidates)
        {
            var parsed = candidates
                .Select(b => (Band: b, From: Number(b.AgeFrom), To: Number(b.AgeTo)))
                .Where(x => x.From is not null)
                .ToList();
            if (!age.InDays)
                return parsed.Where(x => x.To is not null && x.From <= age.Value && x.To >= age.Value)
                    .OrderBy(x => x.From).Select(x => x.Band).FirstOrDefault();
            return parsed.Where(x => x.From <= age.Value)
                .OrderBy(x => x.From).Select(x => x.Band).FirstOrDefault();
        }
    }

    private static int? Number(string? ageLimit)
    {
        ageLimit = ageLimit?.Trim();
        if (string.IsNullOrEmpty(ageLimit) || ageLimit.Length < 2) return null;
        return int.TryParse(ageLimit[..^1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    /// <summary>Pro-rates an annual amount over the inclusive day range (legacy InsertPremium / InsertMCOFees).</summary>
    public static decimal ProRate(DateTime effective, DateTime expiry, decimal annualAmount) =>
        ((expiry.Date - effective.Date).Days + 1) * annualAmount / 365m;

    /// <summary>Money rounding used on screen: 2 decimals, away from zero.</summary>
    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>VB6 Round() to a whole number (banker's rounding) applied after the 2-decimal display format.</summary>
    public static decimal RoundWhole(decimal value) => Math.Round(Money(value), 0, MidpointRounding.ToEven);
}
