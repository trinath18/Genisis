using System.Globalization;
using Genisis.Api.Membership;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Genisis.Api.LeafletCard;

/// <summary>
/// The leaflet and card layouts of Leaflet.rpt and MBMCardPrinting.rpt, rendered as PDF (Crystal Reports 7 is not available).
/// Fields follow the reports: the leaflet shows the member's address, membership and policy number (agent code in brackets), plan,
/// payor, group company, employee number, department and, for family plans, the supplementary members; each card shows one covered
/// person.
/// </summary>
public static class LeafletCardPdf
{
    private const string FontName = "Arial";
    private const string DateFormat = "dd/MM/yyyy";
    /// <summary>ID-1 card size (ISO/IEC 7810), the card printer's stock.</summary>
    private static readonly Unit CardWidth = Unit.FromMillimeter(85.6);
    private static readonly Unit CardHeight = Unit.FromMillimeter(54);

    static LeafletCardPdf()
    {
        // PDFsharp 6 needs a font source; the API runs on Windows, whose fonts are used.
        if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private static string Date(DateTime? value) => value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "";

    /// <summary>Leaflet.rpt formula: policy number followed by "(agent code)" when there is an agent code.</summary>
    public static string PolicyWithAgent(string? policyNo, string? agentCode) =>
        (policyNo?.Trim() ?? "") + (string.IsNullOrWhiteSpace(agentCode) ? "" : $"({agentCode.Trim()})");

    public static byte[] Leaflets(IReadOnlyList<Leaflet> leaflets)
    {
        var document = NewDocument("Membership Leaflets");
        foreach (var leaflet in leaflets)
        {
            var section = document.AddSection();
            section.PageSetup = document.DefaultPageSetup.Clone();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(2);

            foreach (var line in AddressLines(leaflet)) section.AddParagraph(line);
            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(1);

            var title = section.AddParagraph(leaflet.PayorName ?? "");
            title.Format.Font.Size = 14;
            title.Format.Font.Bold = true;
            section.AddParagraph("Membership Leaflet").Format.SpaceAfter = Unit.FromPoint(10);

            var details = section.AddTable();
            details.AddColumn(Unit.FromCentimeter(5));
            details.AddColumn(Unit.FromCentimeter(12));
            void Detail(string label, string? value)
            {
                var row = details.AddRow();
                row.Cells[0].AddParagraph(label).Format.Font.Bold = true;
                row.Cells[1].AddParagraph(value ?? "");
            }
            Detail("Membership No", leaflet.MembershipNo);
            Detail("Policy No", PolicyWithAgent(leaflet.PolicyNo, leaflet.AgentCode));
            Detail("Name", leaflet.Name);
            Detail("Plan", leaflet.PlanDescription);
            Detail("Group Company", leaflet.GroupCompany);
            Detail("Employee No", leaflet.EmployeeNo);
            Detail("Department", leaflet.Department);

            if (InsuredTypes.IsFamily(leaflet.InsuredType) && leaflet.Covered.Count > 0)
            {
                var heading = section.AddParagraph("Supplementary Card");
                heading.Format.Font.Bold = true;
                heading.Format.SpaceBefore = Unit.FromPoint(14);
                heading.Format.SpaceAfter = Unit.FromPoint(4);
                var table = section.AddTable();
                table.Borders.Width = 0.5;
                table.AddColumn(Unit.FromCentimeter(2));
                table.AddColumn(Unit.FromCentimeter(11));
                table.AddColumn(Unit.FromCentimeter(4));
                var header = table.AddRow();
                header.HeadingFormat = true;
                header.Format.Font.Bold = true;
                header.Cells[0].AddParagraph("Cover");
                header.Cells[1].AddParagraph("Name");
                header.Cells[2].AddParagraph("Relationship");
                foreach (var person in leaflet.Covered)
                {
                    var row = table.AddRow();
                    row.Cells[0].AddParagraph(person.CoverId);
                    row.Cells[1].AddParagraph(person.Name ?? "");
                    row.Cells[2].AddParagraph(person.Relationship ?? "");
                }
            }
        }
        return Render(document);
    }

    /// <summary>The mailing block: name, address lines, postcode and city, state; empty lines are left out.</summary>
    public static IEnumerable<string> AddressLines(Leaflet leaflet) =>
        new[] { leaflet.Name, leaflet.Address1, leaflet.Address2, leaflet.Address3, $"{leaflet.PostCode} {leaflet.City}".Trim(), leaflet.State }
            .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line!.Trim());

    public static byte[] Cards(string payorName, IReadOnlyList<MembershipCard> cards)
    {
        var document = NewDocument("Membership Cards");
        foreach (var card in cards)
        {
            var section = document.AddSection();
            section.PageSetup = document.DefaultPageSetup.Clone();
            section.PageSetup.PageWidth = CardWidth;
            section.PageSetup.PageHeight = CardHeight;
            section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromMillimeter(4);
            section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromMillimeter(5);

            var payor = section.AddParagraph(payorName);
            payor.Format.Font.Size = 8;
            payor.Format.Font.Bold = true;
            payor.Format.SpaceAfter = Unit.FromPoint(4);
            var name = section.AddParagraph(card.Name ?? "");
            name.Format.Font.Size = 9;
            name.Format.Font.Bold = true;
            foreach (var line in CardLines(card))
                section.AddParagraph(line).Format.Font.Size = 7;
        }
        return Render(document);
    }

    /// <summary>The fields printed under the member's name.</summary>
    public static IEnumerable<string> CardLines(MembershipCard card) =>
    [
        $"IC No: {card.IcBcPp}",
        $"Member No: {card.CardNumber}",
        $"Policy No: {card.PolicyNo}",
        $"Plan: {card.PlanCode}   Expiry: {Date(card.ExpiryDate)}",
        $"{card.GroupCompany}",
        string.IsNullOrWhiteSpace(card.EmployeeNo) ? "" : $"Employee No: {card.EmployeeNo}",
    ];

    private static Document NewDocument(string title)
    {
        var document = new Document();
        document.Info.Title = title;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontName;
        normal.Font.Size = 10;
        return document;
    }

    private static byte[] Render(Document document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }
}
