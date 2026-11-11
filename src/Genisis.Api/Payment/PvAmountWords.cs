using System.Globalization;

namespace Genisis.Api.Payment;

/// <summary>The amount of a payment voucher in words, as the desktop printed it on the PV and the cheque (<c>CurrencyConversion.bas</c>, <c>ConvertCurrencyToEnglish</c>), split over the two lines
/// the vouchers keep (<c>PVAmtWording</c> and <c>PVAmtWordingTwo</c>).</summary>
/// <remarks>
/// The routine is ported as it is, double spaces included ("Seven Hundred  And Sixty"). What the forms did with its result is read from the 24,600 hospital vouchers: a leading "And" is dropped (no voucher
/// starts with one), " Only" is added, and when the whole text is longer than 55 characters the ringgit stay on the first line and the cents go to the second. One change: a long amount with no cents lost
/// its " Only" on the desktop (136 vouchers); it is written on the second line.
/// </remarks>
public static class PvAmountWords
{
    /// <summary>The longest text the first line takes when the amount is not split.</summary>
    public const int LineLength = 55;

    private static readonly string[] Place = ["", "", " Thousand ", " Million ", " Billion ", " Trillion "];

    /// <summary>The two lines of the amount in words (the second is empty when the text fits on one line).</summary>
    public static (string Line1, string Line2) Lines(decimal amount)
    {
        var (dollars, cents) = Parts(amount);
        var whole = dollars + cents + " Only";
        if (whole.Length <= LineLength) return (whole, "");
        return (dollars, cents + " Only");
    }

    /// <summary>The ringgit and the cents parts, as <c>ConvertCurrencyToEnglish</c> built them (the cents part starts with " And Cents ").</summary>
    public static (string Dollars, string Cents) Parts(decimal amount)
    {
        var number = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        var cents = "";
        var dot = number.IndexOf('.');
        if (dot >= 0)
        {
            cents = Tens((number[(dot + 1)..] + "00")[..2]);
            number = number[..dot];
        }

        var dollars = "";
        for (var count = 1; number.Length > 0 && count < Place.Length; count++)
        {
            var temp = Hundreds(number.Length > 3 ? number[^3..] : number);
            if (temp != "") dollars = temp + Place[count] + dollars;
            number = number.Length > 3 ? number[..^3] : "";
        }

        if (dollars == "One") dollars = "One Dollar";
        if (dollars.StartsWith("And ", StringComparison.Ordinal)) dollars = dollars[4..];

        cents = cents switch
        {
            "" => "",
            "One" => " And  Cent One",
            _ => " And Cents " + cents,
        };
        return (dollars, cents);
    }

    private static string Hundreds(string number)
    {
        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value == 0) return "";
        number = ("000" + number)[^3..];
        var result = "";
        if (number[0] != '0') result = Digit(number[0]) + " Hundred ";
        result += number[1] != '0' ? " And " + Tens(number[1..]) : Digit(number[2]);
        return result.Trim();
    }

    private static string Tens(string tens)
    {
        if (tens[0] == '1')
        {
            return tens switch
            {
                "10" => "Ten", "11" => "Eleven", "12" => "Twelve", "13" => "Thirteen", "14" => "Fourteen", "15" => "Fifteen", "16" => "Sixteen", "17" => "Seventeen",
                "18" => "Eighteen", "19" => "Nineteen", _ => "",
            };
        }

        var result = tens[0] switch
        {
            '2' => "Twenty ", '3' => "Thirty ", '4' => "Forty ", '5' => "Fifty ", '6' => "Sixty ", '7' => "Seventy ", '8' => "Eighty ", '9' => "Ninety ", _ => "",
        };
        return result + Digit(tens[^1]);
    }

    private static string Digit(char digit) => digit switch
    {
        '1' => "One", '2' => "Two", '3' => "Three", '4' => "Four", '5' => "Five", '6' => "Six", '7' => "Seven", '8' => "Eight", '9' => "Nine", _ => "",
    };
}
