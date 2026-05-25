using System.Globalization;
using ClosedXML.Excel;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services;

public interface ILeadExcelExportService
{
    byte[] BuildWorkbook(IReadOnlyList<LeadExcelExportRow> rows);
}

public sealed class LeadExcelExportRow
{
    public required string ClientName { get; init; }
    public required string PhoneNumber { get; init; }
    public required string LeadSourceLabel { get; init; }
    public required string StatusLabel { get; init; }
    public required string AssignedToName { get; init; }
    public required DateTime AssignmentDateUtc { get; init; }
  /// <summary>Follow-ups in chronological order (oldest first).</summary>
    public IReadOnlyList<LeadExcelFollowUpCell> FollowUps { get; init; } = [];
}

public sealed class LeadExcelFollowUpCell
{
    public required DateTime FollowUpDateUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed class LeadExcelExportService : ILeadExcelExportService
{
    private static readonly CultureInfo ExportCulture = CultureInfo.GetCultureInfo("en-IN");

    public byte[] BuildWorkbook(IReadOnlyList<LeadExcelExportRow> rows)
    {
        var maxFollowUps = rows.Count == 0 ? 0 : rows.Max(r => r.FollowUps.Count);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leads");

        var col = 1;
        sheet.Cell(1, col++).Value = "Client Name";
        sheet.Cell(1, col++).Value = "Client Phone Number";
        sheet.Cell(1, col++).Value = "Lead Source";
        sheet.Cell(1, col++).Value = "Lead Status";
        sheet.Cell(1, col++).Value = "Assigned To";
        sheet.Cell(1, col++).Value = "Lead Assignment Date";

        for (var i = 1; i <= maxFollowUps; i++)
        {
            sheet.Cell(1, col++).Value = $"Follow-up {i} Date";
            sheet.Cell(1, col++).Value = $"Follow-up {i} Note";
        }

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#3366CC");
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Alignment.WrapText = true;

        var rowIndex = 2;
        foreach (var lead in rows)
        {
            col = 1;
            sheet.Cell(rowIndex, col++).Value = lead.ClientName;
            sheet.Cell(rowIndex, col++).Value = lead.PhoneNumber;
            sheet.Cell(rowIndex, col++).Value = lead.LeadSourceLabel;
            sheet.Cell(rowIndex, col++).Value = lead.StatusLabel;
            sheet.Cell(rowIndex, col++).Value = lead.AssignedToName;
            sheet.Cell(rowIndex, col).Value = lead.AssignmentDateUtc;
            sheet.Cell(rowIndex, col).Style.DateFormat.Format = "dd-mmm-yyyy";
            col++;

            for (var i = 0; i < maxFollowUps; i++)
            {
                if (i < lead.FollowUps.Count)
                {
                    var fu = lead.FollowUps[i];
                    sheet.Cell(rowIndex, col).Value = fu.FollowUpDateUtc;
                    sheet.Cell(rowIndex, col).Style.DateFormat.Format = "dd-mmm-yyyy";
                    col++;
                    sheet.Cell(rowIndex, col++).Value = fu.Notes ?? string.Empty;
                }
                else
                {
                    sheet.Cell(rowIndex, col++).Value = string.Empty;
                    sheet.Cell(rowIndex, col++).Value = string.Empty;
                }
            }

            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string FormatLeadSource(LeadSource source) =>
        source switch
        {
            LeadSource.Website => "Website",
            LeadSource.Referral => "Referral",
            LeadSource.SocialMedia => "Social Media",
            LeadSource.DirectCall => "Direct Call",
            LeadSource.Email => "Email",
            LeadSource.WalkIn => "Walk In",
            LeadSource.Advertisement => "Advertisement",
            LeadSource.Other => "Other",
            _ => source.ToString()
        };

    public static string FormatLeadStatus(LeadStatus status) =>
        status switch
        {
            LeadStatus.Matured => "Matured",
            LeadStatus.NotInterested => "Not Interested",
            LeadStatus.NoResponse => "No Response",
            LeadStatus.Cancelled => "Cancelled",
            LeadStatus.Confirmed => "Confirmed",
            LeadStatus.PackageSent => "Package Sent",
            LeadStatus.Followup => "Follow-up",
            LeadStatus.AlreadyBooked => "Already Booked",
            LeadStatus.New => "New",
            _ => status.ToString()
        };

    public static string FormatDateForFileName(DateTime utc) =>
        utc.ToString("yyyy-MM-dd", ExportCulture);
}
