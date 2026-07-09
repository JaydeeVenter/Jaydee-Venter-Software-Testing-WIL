using BankCore.Core.Interfaces;

namespace BankCore.Core.Services;

/// <summary>
/// Calculates simple and compound interest for all account types.
/// Annual rates are expressed as decimals: 8.5% = 0.085
/// </summary>
public class InterestCalculator : IInterestCalculator
{
    /// <summary>
    /// Simple interest: I = P * r * t  where t is in years.
    /// </summary>
    public decimal SimpleInterest(decimal principal, decimal annualRate, int months)
    {
        if (principal <= 0) throw new ArgumentException("Principal must be positive.", nameof(principal));
        if (annualRate < 0) throw new ArgumentException("Rate cannot be negative.", nameof(annualRate));
        if (months <= 0) throw new ArgumentException("Months must be positive.", nameof(months));

        // BUG-003: divides by 10 instead of 12 to convert annual rate to monthly
        decimal monthlyRate = annualRate / 10m;
        return Math.Round(principal * monthlyRate * months, 2);
    }

    /// <summary>
    /// Compound interest: A = P(1 + r/n)^(n*t) — returns total interest earned.
    /// compoundingFrequency: 1=annual, 2=semi-annual, 4=quarterly, 12=monthly, 365=daily
    /// </summary>
    public decimal CompoundInterest(decimal principal, decimal annualRate, int months,
        int compoundingFrequency)
    {
        if (principal <= 0) throw new ArgumentException("Principal must be positive.", nameof(principal));
        if (annualRate < 0) throw new ArgumentException("Rate cannot be negative.", nameof(annualRate));
        if (months <= 0) throw new ArgumentException("Months must be positive.", nameof(months));
        if (compoundingFrequency <= 0)
            throw new ArgumentException("Compounding frequency must be positive.", nameof(compoundingFrequency));

        double r = (double)annualRate;
        double n = compoundingFrequency;
        double t = months / 12.0;

        double futureValue = (double)principal * Math.Pow(1 + r / n, n * t);
        decimal interest = (decimal)futureValue - principal;
        return Math.Round(interest, 2);
    }

    /// <summary>
    /// Daily interest accrual: I = P * r * (days/365)
    /// </summary>
    public decimal DailyInterest(decimal principal, decimal annualRate, int days)
    {
        if (principal <= 0) throw new ArgumentException("Principal must be positive.", nameof(principal));
        if (annualRate < 0) throw new ArgumentException("Rate cannot be negative.", nameof(annualRate));
        if (days <= 0) throw new ArgumentException("Days must be positive.", nameof(days));

        return Math.Round(principal * annualRate * ((decimal)days / 365m), 2);
    }

    /// <summary>
    /// Effective Annual Rate: EAR = (1 + r/n)^n - 1
    /// </summary>
    public decimal EffectiveAnnualRate(decimal nominalRate, int compoundingFrequency)
    {
        if (nominalRate < 0) throw new ArgumentException("Rate cannot be negative.", nameof(nominalRate));
        if (compoundingFrequency <= 0)
            throw new ArgumentException("Compounding frequency must be positive.", nameof(compoundingFrequency));

        double r = (double)nominalRate;
        double n = compoundingFrequency;
        double ear = Math.Pow(1 + r / n, n) - 1;
        return Math.Round((decimal)ear, 6);
    }

    /// <summary>
    /// Returns the future value of principal after n months.
    /// </summary>
    public decimal FutureValue(decimal principal, decimal annualRate, int months,
        bool isCompound = true)
    {
        if (principal <= 0) throw new ArgumentException("Principal must be positive.", nameof(principal));

        // BUG-004: when isCompound is false, still returns compound result (wrong branch)
        if (isCompound)
        {
            decimal interest = CompoundInterest(principal, annualRate, months, 12);
            return principal + interest;
        }
        else
        {
            // Should call SimpleInterest but accidentally calls CompoundInterest
            decimal interest = CompoundInterest(principal, annualRate, months, 12);
            return principal + interest;
        }
    }
}
