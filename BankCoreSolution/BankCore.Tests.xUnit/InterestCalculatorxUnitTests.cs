using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using BankCore.Core.Services;
using System.Runtime.CompilerServices;

namespace BankCore.Tests.xUnit
{
    public class CalculatorFixture : IDisposable
    {
        public InterestCalculator Calculator { get; private set; }

        public CalculatorFixture()
        {
            Calculator = new InterestCalculator();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class FinancialPrecisionComparer : IEqualityComparer<decimal>
    {
        public bool Equals(decimal x, decimal y) => Math.Abs(x - y) <= 0.01m;
        public int GetHashCode(decimal obj) => Math.Round(obj, 2).GetHashCode();
    }
    public class SimpleInterestClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] 
            { 
                1000m, 
                0.05m, 
                12, 
                50.00m 
            };
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [CollectionDefinition("InterestCalculatorCollection")]
    public class InterestCalculatorCollectionFixture : ICollectionFixture<CalculatorFixture>
    {

    }

    //Simple Interest Tests

    [Collection("InterestCalculatorCollection")]
    public class SimpleInterestTests
    {
        private readonly InterestCalculator Calculator;

        public SimpleInterestTests(CalculatorFixture fixture)
        {
            Calculator = fixture.Calculator;
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void SimpleInterestTest_11PercentInterest_OverOneYear()
        {
            //Arrange
            decimal principalAmount = 15000m;
            decimal annualRate = 0.11m;
            int periodInMonths = 12;
            decimal expectedInterest = 1650.00m;

            //Act
            var result = Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths);

            //Assert
            result.Should().BeApproximately(expectedInterest, 0.01m);
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void SimpleInterestCalculation_With0PercentAnnualRate()
        {
            //Act
            var result = Calculator.SimpleInterest(15000m, 0m, 12);

            //Assert
            result.Should().Be(0m);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void SimpleIntersestCalculation_With0Months_ShouldReturn0()
        {
            //Act 
            var result = Calculator.SimpleInterest(200000m, 0.09m, 0);

            //Assert
            result.Should().Be(0m);
        }

        [Theory]
        [Trait("Category", "Positive")]
        [ClassData(typeof(SimpleInterestClassData))]
        public void SimpleInterest_UsingClassData_CalculatesCorrectly(decimal principal, decimal rate, int months, decimal expected)
        {
            //act
            var result = Calculator.SimpleInterest(principal, rate, months);

            //assert
            result.Should().BeApproximately(expected, 0.01m);
        }

        [Theory]
        [Trait("Category", "Positive")]
        [InlineData(10000, 0.05, 12, 500.00)]
        [InlineData(15000, 0.05, 6, 375.00)]
        [InlineData(1000, 0.05, 24, 100.00)]
        [InlineData(35000, 0.12, 12, 4200.00)]
        [InlineData(5000, 0.12, 3, 150.00)]
        [InlineData(100000, 0.01, 12, 1000.00)]
        [InlineData(100000, 0.001, 12, 100)]
        [InlineData(10000, 0.0001, 12, 1.00)]
        [InlineData(10000, 1.00, 12, 10000.00)]
        [InlineData(365, 0.08, 1, 2.43)]
        [InlineData(250, 0.08, 18, 30.00)]
        public void SimpleInterest_VariousCombinations_ReturnsEachExpected(decimal principalAmount, decimal annualRate, int periodInMonths, decimal expectedInterest)
        {
            //Act
            var result = Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths);

            //Assert
            result.Should().BeApproximately(expectedInterest, 0.01m);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void SimpleInterest_ReturnsError_WhenPrincipalIsNegative()
        {
            //Arrange
            decimal principalAmount = -1500m;
            decimal annualRate = 0.11m;
            int periodInMonths = 12;

            //Act
            Action act = () => Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths);

            //Assert
            act.Should().Throw<ArgumentException>().WithMessage("*Principal must be positive.*");
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void SimpleInterest_ReturnsError_WhenRateIsNegative()
        {
            //Arrange
            decimal principalAmount = 1500m;
            decimal annualRate = -0.11m;
            int periodInMonths = 12;

            //Act
            Action act = () => Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths);

            //Assert
            act.Should().Throw<ArgumentException>().WithMessage("*Rate cannot be negative.*");
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void SimpleInterest_ReturnsError_WhenMonthsIsNegative()
        {
            //Arrange
            decimal principalAmount = 5000m;
            decimal annualRate = 0.11m;
            int periodInMonths = -12;

            //Act
            Action act = () => Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths);

            //Assert
            act.Should().Throw<ArgumentException>().WithMessage("*Months must be positive.*");
        }

        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(1000, -0.05, 12)] // Negative Rate
        [InlineData(-1000, 0.05, 12)] // Negative Principal
        [InlineData(1000, 0.05, -1)]  // Negative Months
        public void SimpleInterest_InvalidInputs(decimal principalAmount, decimal annualRate, int periodInMonths)
        {
            //Assert
            Assert.Throws<ArgumentException>(() => Calculator.SimpleInterest(principalAmount, annualRate, periodInMonths));
        }
    }

    //Compound Interest Tests
    [Collection("InterestCalculatorCollection")]
    public class CompoundInterestTests
    {
        private readonly InterestCalculator Calculator;

        public CompoundInterestTests(CalculatorFixture fixture)
        {
            Calculator = fixture.Calculator;
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void CompoundInterest_AnnualRate_0_Returns0()
        {
            //Act
            var result = Calculator.CompoundInterest(10000m, 0m, 12, 12);

            //Assert
            result.Should().Be(0m);
        }

        public static IEnumerable<object[]> CompoundInterest_FrequencyScenarious => new List<object[]>
        {
            new object[]
            {
                100000m,
                0.12m,
                12,
                365
            },
            new object[]
            {
                100000m,
                0.12m,
                12,
                12
            },
            new object[]
            {
                100000m,
                0.12m,
                12,
                4
            },
            new object[]
            {
                100000m,
                0.12m,
                12,
                1
            },
        };

        [Theory]
        [Trait("Category", "Positive")]
        [MemberData(nameof(CompoundInterest_FrequencyScenarious))]
        public void VariousFreq_Scenarios_CompoundingInterest(decimal principalAmount, decimal annualRate, int periodInMonths, int frequenceInMonths)
        {

            //Acr
            var result = Calculator.CompoundInterest(principalAmount, annualRate, periodInMonths, frequenceInMonths);

            //Assert
            result.Should().BeGreaterThan(0m);
            result.Should().BeLessThan(principalAmount);
            (principalAmount + result).Should().BeGreaterThan(principalAmount);
        }
        [Theory]
        [Trait("Category", "BVA")]
        //Savings ACC
        [InlineData(1000, 0.05, 12, 12, 51.16)]
        [InlineData(5000, 0.06, 36, 12, 983.40)]
        //Fixed DEposit
        [InlineData(1000, 0.08, 12, 1, 80.00)]
        [InlineData(10000, 0.07, 60, 1, 4025.52)]
        //Notice
        [InlineData(1000, 0.06, 12, 365, 61.83)]
        [InlineData(5000, 0.07, 24, 365, 751.29)]
        //Current Account
        [InlineData(1000, 0.02, 12, 4, 20.15)]
        [InlineData(10000, 0.03, 36, 4, 938.07)]
        //BVA's
        [InlineData(1000, 0.00, 12, 12, 0.00)]
        [InlineData(1000, 0.0001, 12, 12, 0.10)]
        [InlineData(1000, 1.00, 12, 12, 1613.04)]
        public void CompoundInterest_Calculator_WithDifferentAccTypes(decimal principalAmount, decimal rate, int periodInMonths, int freq, decimal expectedValue)
        {
            //act
            var result = Calculator.CompoundInterest(principalAmount, rate, periodInMonths, freq);

            //Assert
            Assert.Equal(expectedValue, result, new FinancialPrecisionComparer());
            result.Should().BeApproximately(expectedValue, 0.01m);
        }

        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(-1000.0, 0.05, 12, 12)]
        [InlineData(1000.0, -0.05, 12, 12)] 
        [InlineData(1000.0, 0.05, -1, 12)] 
        [InlineData(1000.0, 0.05, 12, -1)] 
        [InlineData(1000.0, 0.05, 12, 0)]   
        public void CompoundInterest_InvalInputs_ThrowsArgException(double principalAmount, double annualRate, int periodInMonths, int Freq)
        {
            // Act
            Action result = () => Calculator.CompoundInterest((decimal)principalAmount, (decimal)annualRate, periodInMonths, Freq);

            // Assert
            result.Should().Throw<ArgumentException>();
        }
    }

    //Daily Interest Test
    [Collection("InterestCalculatorCollection")]
    public class DailyInterestTests
    {
        private readonly InterestCalculator Calculator;

        public DailyInterestTests(CalculatorFixture fixture)
        {
            Calculator = fixture.Calculator;
        }
        [Fact]
        [Trait("Category", "Leap Year Test")]
        public void DailyIntTest_DuringLeapYear_CalculatesCorrectly()
        {
            //Arrabnge
            decimal principalAmount = 15000m;
            decimal annualRate = 0.07m;
            int periodInDays = 29;
            decimal expectedInterest = 83.42m;

            //Act
            var result = Calculator.DailyInterest(principalAmount, annualRate, periodInDays);

            //Assert
            result.Should().BeApproximately(expectedInterest, 0.01m);
        }

        [Theory]
        [Trait("Category", "Positive")]
        [InlineData(1000, 0.08, 1, 0.22)]
        [InlineData(1000, 0.08, 30, 6.57)]
        [InlineData(10000, 0.08, 10, 21.91)]
        [InlineData(1000, 0.05, 29, 3.97)]
        [InlineData(10000, 0.08, 365, 800.00)]
        public void DailyInterestTests_MultipleValues_CalculatesCorrectly(decimal principalAmount,  decimal annualRate, int periodInDays, decimal expectedValue)
        {
            //Act
            var result = Calculator.DailyInterest(principalAmount, annualRate, periodInDays);

            //Assert
            result.Should().BeApproximately(expectedValue, 0.01m);
        }

        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(-1000.0, 0.05, 30)]
        [InlineData(1000.0, -0.05, 30)]
        [InlineData(1000.0, 0.05, -1)]
        [InlineData(1000.0, 0.05, 0)]
        public void DailyInterest_InvalInputs_ThrowsArxception(double principalAmount, double annualRate, int periodInDays)
        {
            // Act
            Action result = () => Calculator.DailyInterest((decimal)principalAmount, (decimal)annualRate, periodInDays);

            // Assert
            result.Should().Throw<ArgumentException>();
        }
    }

    //Effective Annual Rate Tests

    [Collection("InterestCalculatorCollection")]
    public class EffectiveAnnualRateTests
    {
        private readonly InterestCalculator Calculator;

        public EffectiveAnnualRateTests(CalculatorFixture fixture)
        {
            Calculator = fixture.Calculator;
        }
        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(-0.10, 12, "Rate cannot be negative.")]
        public void EffectiveAnnualRate_InvalidRate_ThrowsException(decimal annualRate, int Frequency, string exceptionMessage)
        {
            //Arrange and Act
            Action act = () => Calculator.EffectiveAnnualRate(annualRate, Frequency);

            //Assert
            act.Should().Throw<ArgumentException>().WithMessage($"*{exceptionMessage}*");
        }

        [Theory]
        [Trait("Category", "Positive")]
        [InlineData(0.10, 1, 0.100000)]
        [InlineData(0.10, 2, 0.102500)]
        [InlineData(0.10, 4, 0.103813)]
        [InlineData(0.10, 12, 0.104713)]
        [InlineData(0.10, 365, 0.105156)]
        public void EffectiveAnnualRate_ValidInputs(decimal annualRate, int Frequency, decimal expectedRate)
        {
            //Act
            var Result = Calculator.EffectiveAnnualRate(annualRate, Frequency);

            //Assert
            Result.Should().Be(expectedRate);
        }
    }

    //Future Value Tests
    public class FutureValueTests : IClassFixture<CalculatorFixture>
    {
        private readonly InterestCalculator Calculator;
        public FutureValueTests(CalculatorFixture fixture)
        {
            Calculator = fixture.Calculator;
        }

        [Theory]
        [Trait("Category", "Positive")]
        [InlineData(1000, 0.12, 12, true, 1126.83)]
        [InlineData(1000, 0.12, 12, false, 1120.00)]
        public void FutureValue_SimnpleInt_And_CompoundInt_CalculatesCorrectly(decimal principalAmount, decimal annualRate, int periodInMonths, bool intType, decimal expectedValue)
        {
            //act
            var result = Calculator.FutureValue(principalAmount, annualRate, periodInMonths, intType);

            //assert
            result.Should().BeApproximately(expectedValue, 0.01m);
        }

        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(0, 0.10, 12)]
        [InlineData(-100000, 0.10, 12)]
        public void FutureValue_Tests_InvalidPrincipalAmount(decimal principalAmount, decimal rate, int period)
        {
            //Act
            Action act = () => Calculator.FutureValue(principalAmount, rate, period, true);

            //assert
            act.Should().Throw<ArgumentException>().WithMessage("*Principal must be positive.*");
        }
    }
}


