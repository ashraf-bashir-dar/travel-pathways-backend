using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services;

public interface ILeadExcelImportService
{
    byte[] BuildTemplate();
    Task<LeadImportResult> ImportAsync(Stream fileStream, Guid tenantId, string createdBy, CancellationToken ct);
}

public sealed class LeadImportResult
{
    public required int ImportedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int FailedCount { get; init; }
    public List<LeadImportRowError> Errors { get; init; } = [];
}

public sealed class LeadImportRowError
{
    public required int RowNumber { get; init; }
    public required string Message { get; init; }
}

public sealed class LeadExcelImportService : ILeadExcelImportService
{
    private const int MaxRows = 500;

    private static readonly string[] TemplateHeaders =
    [
        "Client Name",
        "Phone Number",
        "Email",
        "State",
        "City",
        "Address",
        "Lead Source",
        "Notes",
        "Assigned To Email"
    ];

    private readonly AppDbContext _db;

    public LeadExcelImportService(AppDbContext db) => _db = db;

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leads");

        for (var c = 0; c < TemplateHeaders.Length; c++)
            sheet.Cell(1, c + 1).Value = TemplateHeaders[c];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#3366CC");
        headerRow.Style.Font.FontColor = XLColor.White;

        sheet.Cell(2, 1).Value = "Jane Doe";
        sheet.Cell(2, 2).Value = "9876543210";
        sheet.Cell(2, 3).Value = "jane@example.com";
        sheet.Cell(2, 4).Value = "Maharashtra";
        sheet.Cell(2, 5).Value = "Mumbai";
        sheet.Cell(2, 6).Value = "123 Main Street";
        sheet.Cell(2, 7).Value = "Website";
        sheet.Cell(2, 8).Value = "Imported lead";
        sheet.Cell(2, 9).Value = "sales@yourcompany.com";

        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Required columns: Client Name, Phone Number.";
        instructions.Cell(2, 1).Value = "Lead Source values: Website, Referral, SocialMedia, DirectCall, Email, WalkIn, Advertisement, Other.";
        instructions.Cell(3, 1).Value = "Assigned To Email must match an active user in your tenant (optional).";
        instructions.Cell(4, 1).Value = $"Maximum {MaxRows} data rows per import. Delete the sample row before importing.";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<LeadImportResult> ImportAsync(
        Stream fileStream,
        Guid tenantId,
        string createdBy,
        CancellationToken ct)
    {
        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Leads", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < 2)
        {
            return new LeadImportResult
            {
                ImportedCount = 0,
                SkippedCount = 0,
                FailedCount = 0,
                Errors = [new LeadImportRowError { RowNumber = 0, Message = "The spreadsheet has no data rows." }]
            };
        }

        var columnMap = BuildColumnMap(sheet, 1);
        if (!columnMap.ContainsKey("clientname") || !columnMap.ContainsKey("phonenumber"))
        {
            return new LeadImportResult
            {
                ImportedCount = 0,
                SkippedCount = 0,
                FailedCount = 0,
                Errors =
                [
                    new LeadImportRowError
                    {
                        RowNumber = 1,
                        Message = "Missing required columns. Use the template: Client Name and Phone Number headers are required."
                    }
                ]
            };
        }

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
            .ToListAsync(ct);

        var userByEmail = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        var userByFullName = users
            .Select(u => new
            {
                u.Id,
                FullName = $"{u.FirstName} {u.LastName}".Trim().ToLowerInvariant()
            })
            .Where(x => x.FullName.Length > 0)
            .GroupBy(x => x.FullName)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var leadsToAdd = new List<Lead>();
        var errors = new List<LeadImportRowError>();
        var skipped = 0;
        var failed = 0;
        var dataRowCount = 0;

        for (var row = 2; row <= lastRow; row++)
        {
            if (dataRowCount >= MaxRows)
            {
                errors.Add(new LeadImportRowError
                {
                    RowNumber = row,
                    Message = $"Import limited to {MaxRows} rows. Remaining rows were not processed."
                });
                break;
            }

            var clientName = GetCell(sheet, row, columnMap, "clientname");
            var phone = GetCell(sheet, row, columnMap, "phonenumber");
            var email = GetOptionalCell(sheet, row, columnMap, "clientemail");
            var state = GetOptionalCell(sheet, row, columnMap, "clientstate");
            var city = GetOptionalCell(sheet, row, columnMap, "clientcity");
            var address = GetOptionalCell(sheet, row, columnMap, "address");
            var sourceText = GetOptionalCell(sheet, row, columnMap, "leadsource");
            var notes = GetOptionalCell(sheet, row, columnMap, "notes");
            var assigneeEmail = GetOptionalCell(sheet, row, columnMap, "assignedtoemail");
            var assigneeName = GetOptionalCell(sheet, row, columnMap, "assignedtoname");

            if (IsEmptyRow(clientName, phone, email, state, city, address, sourceText, notes, assigneeEmail, assigneeName))
            {
                skipped++;
                continue;
            }

            dataRowCount++;

            if (string.IsNullOrWhiteSpace(clientName))
            {
                failed++;
                if (errors.Count < 50)
                    errors.Add(new LeadImportRowError { RowNumber = row, Message = "Client Name is required." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                failed++;
                if (errors.Count < 50)
                    errors.Add(new LeadImportRowError { RowNumber = row, Message = "Phone Number is required." });
                continue;
            }

            if (!TryParseLeadSource(sourceText, out var leadSource))
            {
                failed++;
                if (errors.Count < 50)
                    errors.Add(new LeadImportRowError
                    {
                        RowNumber = row,
                        Message = $"Invalid Lead Source \"{sourceText}\". Use Website, Referral, SocialMedia, etc."
                    });
                continue;
            }

            Guid? assigneeId = null;
            if (!string.IsNullOrWhiteSpace(assigneeEmail))
            {
                var key = assigneeEmail.Trim().ToLowerInvariant();
                if (!userByEmail.TryGetValue(key, out var uid))
                {
                    failed++;
                    if (errors.Count < 50)
                        errors.Add(new LeadImportRowError
                        {
                            RowNumber = row,
                            Message = $"Assigned To Email \"{assigneeEmail}\" does not match an active user."
                        });
                    continue;
                }
                assigneeId = uid;
            }
            else if (!string.IsNullOrWhiteSpace(assigneeName))
            {
                var key = assigneeName.Trim().ToLowerInvariant();
                if (!userByFullName.TryGetValue(key, out var uid))
                {
                    failed++;
                    if (errors.Count < 50)
                        errors.Add(new LeadImportRowError
                        {
                            RowNumber = row,
                            Message = $"Assigned To \"{assigneeName}\" does not match an active user."
                        });
                    continue;
                }
                assigneeId = uid;
            }

            leadsToAdd.Add(new Lead
            {
                TenantId = tenantId,
                ClientName = clientName.Trim(),
                PhoneNumber = phone.Trim(),
                ClientEmail = email?.Trim(),
                ClientState = state?.Trim(),
                ClientCity = city?.Trim(),
                Address = string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim(),
                LeadSource = leadSource,
                Notes = notes?.Trim(),
                Status = LeadStatus.New,
                CreatedBy = createdBy,
                AssignedToUserId = assigneeId,
                NextFollowUpDate = LeadNextFollowUpHelper.DefaultDate()
            });
        }

        if (leadsToAdd.Count > 0)
        {
            _db.Leads.AddRange(leadsToAdd);
            await _db.SaveChangesAsync(ct);
        }

        return new LeadImportResult
        {
            ImportedCount = leadsToAdd.Count,
            SkippedCount = skipped,
            FailedCount = failed,
            Errors = errors
        };
    }

    private static Dictionary<string, int> BuildColumnMap(IXLWorksheet sheet, int headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = sheet.Cell(headerRow, col).GetString().Trim();
            if (string.IsNullOrEmpty(header)) continue;

            var key = MapHeaderToKey(header);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = col;
        }
        return map;
    }

    private static string? MapHeaderToKey(string header)
    {
        var n = NormalizeHeader(header);
        return n switch
        {
            "clientname" or "name" or "client" or "customername" or "customer" => "clientname",
            "phonenumber" or "phone" or "mobile" or "mobilenumber" or "contact" or "contactnumber" => "phonenumber",
            "email" or "clientemail" or "emailaddress" => "clientemail",
            "state" or "clientstate" => "clientstate",
            "city" or "clientcity" => "clientcity",
            "address" => "address",
            "leadsource" or "source" => "leadsource",
            "notes" or "note" or "remarks" or "comment" => "notes",
            "assignedtoemail" or "assigneeemail" or "assignedemail" or "salespersonemail" => "assignedtoemail",
            "assignedto" or "assignedtoname" or "assignee" or "salesperson" or "salesrep" => "assignedtoname",
            _ => null
        };
    }

    private static string NormalizeHeader(string header) =>
        new string(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string GetCell(IXLWorksheet sheet, int row, Dictionary<string, int> map, string key) =>
        map.TryGetValue(key, out var col) ? sheet.Cell(row, col).GetString().Trim() : string.Empty;

    private static string? GetOptionalCell(IXLWorksheet sheet, int row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col)) return null;
        var v = sheet.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static bool IsEmptyRow(params string?[] values) =>
        values.All(string.IsNullOrWhiteSpace);

    private static bool TryParseLeadSource(string? text, out LeadSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            source = LeadSource.Other;
            return true;
        }

        var normalized = text.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (Enum.TryParse<LeadSource>(normalized, ignoreCase: true, out source))
            return true;

        var withSpaces = text.Trim();
        foreach (LeadSource value in Enum.GetValues<LeadSource>())
        {
            if (string.Equals(value.ToString(), withSpaces, StringComparison.OrdinalIgnoreCase))
            {
                source = value;
                return true;
            }
        }

        source = LeadSource.Other;
        return false;
    }
}
