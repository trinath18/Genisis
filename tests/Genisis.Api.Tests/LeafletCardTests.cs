using System.Text;
using Dapper;
using Genisis.Api.LeafletCard;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class LeafletCardTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static LeafletCardCriteria Valid() => new()
    {
        BordxDateFrom = new DateTime(2021, 1, 1), BordxDateTo = new DateTime(2021, 12, 31), PayorCode = "AX", BatchNo = 1, HealthCode = "N",
    };

    [Fact]
    public void Search_messages_follow_the_form_order()
    {
        Assert.Null(LeafletCardValidator.Validate(Valid()));
        Assert.Equal(LeafletCardMessages.EnterDateFrom, LeafletCardValidator.Validate(new LeafletCardCriteria()));
        var c = Valid(); c.BordxDateTo = null; c.PayorCode = null;
        Assert.Equal(LeafletCardMessages.EnterDateTo, LeafletCardValidator.Validate(c));
        c = Valid(); c.PayorCode = " ";
        Assert.Equal(LeafletCardMessages.EnterInsurer, LeafletCardValidator.Validate(c));
        c = Valid(); c.BatchNo = null;
        Assert.Equal(LeafletCardMessages.EnterBatchNo, LeafletCardValidator.Validate(c));
        c = Valid(); c.HealthCode = null;
        Assert.Equal(LeafletCardMessages.EnterHealthType, LeafletCardValidator.Validate(c));
        c = Valid(); c.BordxDateTo = new DateTime(2020, 1, 1);
        Assert.Equal(LeafletCardMessages.DateOrder, LeafletCardValidator.Validate(c));
    }

    [Fact]
    public void Report_formulas_follow_the_crystal_layouts()
    {
        Assert.Equal("P123(AG9)", LeafletCardPdf.PolicyWithAgent(" P123 ", " AG9 "));
        Assert.Equal("P123", LeafletCardPdf.PolicyWithAgent("P123", " "));
        Assert.Equal("AX210105", LeafletCardService.FileName("AX", new DateTime(2021, 1, 5)));
        var leaflet = new Leaflet { Name = "ALI", Address1 = "1 JALAN A", Address2 = " ", PostCode = "50000", City = "KL", State = "WP" };
        Assert.Equal(["ALI", "1 JALAN A", "50000 KL", "WP"], LeafletCardPdf.AddressLines(leaflet));
        var card = new MembershipCard { MembershipNo = "M1", CoverId = "01", ExpiryDate = new DateTime(2026, 12, 31), PlanCode = "BT48" };
        Assert.Equal("M1-01", card.CardNumber);
        Assert.Contains("Plan: BT48   Expiry: 31/12/2026", LeafletCardPdf.CardLines(card));
    }

    [Fact]
    public void Pdfs_have_one_page_per_leaflet_and_per_card()
    {
        var leaflets = new List<Leaflet>
        {
            new() { MembershipNo = "M1", Name = "ALI", InsuredType = "F", Covered = [new() { CoverId = "01", Name = "SITI", Relationship = "W" }] },
            new() { MembershipNo = "M2", Name = "ABU", InsuredType = "I" },
        };
        var leafletPdf = LeafletCardPdf.Leaflets(leaflets);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(leafletPdf, 0, 4));
        Assert.Equal(2, PageCount(leafletPdf));

        var cards = new List<MembershipCard> { new() { MembershipNo = "M1", CoverId = "00", Name = "ALI" }, new() { MembershipNo = "M1", CoverId = "01", Name = "SITI" } };
        Assert.Equal(2, PageCount(LeafletCardPdf.Cards("PAYOR", cards)));
    }

    private static int PageCount(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return PdfSharp.Pdf.IO.PdfReader.Open(stream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import).PageCount;
    }

    [Fact]
    public async Task Leaflets_and_cards_read_a_real_batch()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        var batch = await conn.QueryFirstOrDefaultAsync<(DateTime Bordx, string Payor, string Health, int Batch)?>("""
            SELECT TOP (1) m.MBMBordxDate, RTRIM(m.PAYCode), RTRIM(m.HLTCode), m.MBMBatchNo FROM dbo.MBM m
            JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
            WHERE m.INSCode IN ('F', 'H') AND m.MBMStatus = 'A' AND m.MBMDataStatus = 'A' AND m.MBMBatchNo IS NOT NULL
              AND EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = m.MBMNumber)
            ORDER BY m.MBMBordxDate DESC
            """);
        if (batch is not { } b) return;
        var criteria = new LeafletCardCriteria { BordxDateFrom = b.Bordx, BordxDateTo = b.Bordx, PayorCode = b.Payor, HealthCode = b.Health, BatchNo = b.Batch };

        var leaflets = await LeafletCardService.GetLeafletsAsync(conn, criteria);
        Assert.NotEmpty(leaflets);
        Assert.Contains(leaflets, l => l.Covered.Count > 0);
        Assert.Equal(leaflets.Count, leaflets.Select(l => l.MembershipNo).Distinct().Count());

        var cards = await LeafletCardService.GetCardsAsync(conn, criteria);
        Assert.Contains(cards.Cards, c => c.CoverId == "00");
        Assert.Contains(cards.Cards, c => c.CoverId != "00");
        Assert.Equal(LeafletCardService.FileName(b.Payor, b.Bordx), cards.FileName);
        Assert.True(PageCount(LeafletCardPdf.Cards(cards.PayorName, cards.Cards)) == cards.Cards.Count);
    }
}
