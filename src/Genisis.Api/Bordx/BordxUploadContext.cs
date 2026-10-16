using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Bordx;

/// <summary>State of one upload run: the open transaction, the file header values, the batch and the result counters.</summary>
internal sealed class BordxUploadContext(SqlConnection conn, SqlTransaction tx, IReadOnlyList<BordxLine> lines, BordxSettings settings, BordxFormat format,
    string userCode, string stagingKey, DateTime today, CancellationToken cancellationToken)
{
    public SqlConnection Connection { get; } = conn;
    public SqlTransaction Transaction { get; } = tx;
    public IReadOnlyList<BordxLine> Lines { get; } = lines;
    public BordxSettings Settings { get; } = settings;
    public BordxFormat Format { get; } = format;
    public string UserCode { get; } = userCode;
    /// <summary>TmpUpdatedBy value of this run's rows in the UploadDb staging tables.</summary>
    public string StagingKey { get; } = stagingKey;
    public DateTime Today { get; } = today;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public BordxLine Header => Lines[0];
    public string FileName => Header.Text(HeaderLayout.FileName);
    public string FileStatus => Header.Raw(HeaderLayout.FileStatus);
    public string FilePayor => Header.Raw(HeaderLayout.PayorCode);
    public DateTime SubmissionDate => Header.Date(HeaderLayout.SubmissionDate) ?? throw new BordxException("Invalid Submission Date !");

    /// <summary>Payor of the last ZURICH / TAK plan looked up (desktop tmpPLNPayor; kept when a plan is not found).</summary>
    public string PlanPayor { get; set; } = "";

    public int? Batch { get; private set; }
    public int NewMembers { get; set; }
    public int Supplementaries { get; set; }
    public int Cancelled { get; set; }
    public int Reinstated { get; set; }
    public List<string> MembershipNumbers { get; } = [];
    public List<string> Warnings { get; } = [];

    public async Task AllocateBatchAsync(string payorCode) =>
        Batch = await Connection.ProcSingleAsync<int>("genisis.Bordx_NextBatch",
            new { pay = payorCode, bordx = SubmissionDate, type = Settings.FileType }, Transaction);

    public Task ExecAsync(string procedure, object parameters) => Connection.ProcExecAsync(procedure, parameters, Transaction);

    /// <summary>GST columns are only filled in the ZURICH / TAK layout; payor MX uses its own columns.</summary>
    public (decimal? Amount, decimal? Percent) Gst(BordxLine line)
    {
        if (!Format.IsZurichTakLayout) return (null, null);
        return PlanPayor == SpecialPayors.Mx
            ? (line.Amount(MemberLayout.GstAmountMx), line.Amount(MemberLayout.GstPercentMx))
            : (line.Amount(MemberLayout.GstAmount), line.Amount(MemberLayout.GstPercent));
    }
}
