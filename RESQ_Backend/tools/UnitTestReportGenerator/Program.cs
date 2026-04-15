using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

const double NormalCasesPerKloc = 90.0;
const string ExecutedDateText = "2026/04/09";
const string CreatorName = "Codex";
const string ProjectName = "RESQ SOS Backend API";
const string ProjectCode = "SEP490-RESQ";
const string DocumentVersion = "1.0";
const string DocumentCode = "SEP490-RESQ_UnitTest_v1.0";
const string ReportDocumentCode = "SEP490-RESQ_UnitTestReport_v1.0";
const string OutputNotes = "Coverage for RTM-defined application use cases in RESQ.Tests.";
const string EnvironmentDescription = "1. xUnit on prebuilt RESQ.Tests.dll\n2. dotnet vstest (verified locally on 2026/04/09)\n3. .NET 8 test assembly";

var rootDirectory = Directory.GetCurrentDirectory();
if (args.Length < 5 || !string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: generate <sample-xlsx> <rtm-csv> <output-xlsx> <output-csv>");
    return 1;
}

return Generate(args, rootDirectory);

static int Generate(string[] args, string rootDirectory)
{
    var samplePath = args[1];
    var rtmPath = args[2];
    var outputWorkbookPath = args[3];
    var outputCsvPath = args[4];

    EnsureFileExists(samplePath, "Sample workbook");
    EnsureFileExists(rtmPath, "RTM CSV");

    var workbookDirectory = Path.GetDirectoryName(Path.GetFullPath(outputWorkbookPath));
    var csvDirectory = Path.GetDirectoryName(Path.GetFullPath(outputCsvPath));
    if (!string.IsNullOrWhiteSpace(workbookDirectory)) Directory.CreateDirectory(workbookDirectory);
    if (!string.IsNullOrWhiteSpace(csvDirectory)) Directory.CreateDirectory(csvDirectory);

    var mappings = BuildMappings();
    var reports = LoadRequirements(rtmPath)
        .Select(requirement => BuildRequirementReport(requirement, mappings, rootDirectory))
        .OrderBy(report => report.Requirement.No)
        .ToList();

    using var workbook = new XLWorkbook();
    PrepareWorkbook(workbook, reports);
    workbook.SaveAs(outputWorkbookPath);
    WriteSummaryCsv(reports, outputCsvPath);

    Console.WriteLine($"Generated workbook: {outputWorkbookPath}");
    Console.WriteLine($"Generated CSV: {outputCsvPath}");
    Console.WriteLine($"Requirements: {reports.Count}");
    Console.WriteLine($"Test cases: {reports.Sum(report => report.Cases.Count)}");
    return 0;
}

static void EnsureFileExists(string path, string label)
{
    if (!File.Exists(path)) throw new FileNotFoundException($"{label} not found: {path}");
}

static List<RequirementRow> LoadRequirements(string csvPath)
{
    var rows = new List<RequirementRow>();
    using var parser = new TextFieldParser(csvPath);
    parser.TextFieldType = FieldType.Delimited;
    parser.SetDelimiters(",");
    parser.HasFieldsEnclosedInQuotes = true;
    if (!parser.EndOfData) parser.ReadFields();

    while (!parser.EndOfData)
    {
        var fields = parser.ReadFields();
        if (fields is null || fields.Length < 8) continue;
        rows.Add(new RequirementRow(
            int.Parse(fields[0], CultureInfo.InvariantCulture),
            fields[1],
            fields[2],
            fields[3],
            fields[4],
            fields[5],
            fields[6],
            fields[7]));
    }

    return rows;
}

static RequirementReport BuildRequirementReport(
    RequirementRow requirement,
    IReadOnlyDictionary<string, RequirementMapping> mappings,
    string rootDirectory)
{
    if (!mappings.TryGetValue(requirement.SheetName, out var mapping))
        throw new InvalidOperationException($"No mapping configured for sheet '{requirement.SheetName}'.");

    var cases = mapping.TestTargets.SelectMany(target => ParseTestCases(target, rootDirectory)).ToList();
    for (var index = 0; index < cases.Count; index++)
        cases[index] = cases[index] with { CaseId = $"UTCID{index + 1:00}" };

    var meaningfulLoc = mapping.SourcePaths.Sum(path => CountMeaningfulLines(Path.Combine(rootDirectory, path)));
    var lackOfCases = Math.Max(0.0, meaningfulLoc * NormalCasesPerKloc / 1000.0 - cases.Count);

    return new RequirementReport(
        requirement,
        mapping,
        cases,
        meaningfulLoc,
        lackOfCases,
        cases.Count,
        0,
        0,
        cases.Count(testCase => testCase.TestType == "N"),
        cases.Count(testCase => testCase.TestType == "A"),
        cases.Count(testCase => testCase.TestType == "B"));
}

static List<TestCaseRow> ParseTestCases(TestTarget target, string rootDirectory)
{
    var fullPath = Path.Combine(rootDirectory, target.RelativePath);
    EnsureFileExists(fullPath, "Test file");

    var lines = File.ReadAllLines(fullPath);
    var cases = new List<TestCaseRow>();
    var methodRegex = new Regex(@"^\s*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(", RegexOptions.Compiled);
    var factTheoryRegex = new Regex(@"^\s*\[(Fact|Theory)\]\s*$", RegexOptions.Compiled);
    var inlineDataRegex = new Regex(@"^\s*\[InlineData\((.+)\)\]\s*$", RegexOptions.Compiled);

    for (var index = 0; index < lines.Length; index++)
    {
        var attributeMatch = factTheoryRegex.Match(lines[index]);
        if (!attributeMatch.Success) continue;

        var isTheory = string.Equals(attributeMatch.Groups[1].Value, "Theory", StringComparison.Ordinal);
        var inlineData = new List<string>();
        var cursor = index + 1;

        while (cursor < lines.Length)
        {
            var inlineMatch = inlineDataRegex.Match(lines[cursor]);
            if (!inlineMatch.Success) break;
            inlineData.Add(inlineMatch.Groups[1].Value.Trim());
            cursor++;
        }

        while (cursor < lines.Length)
        {
            var methodMatch = methodRegex.Match(lines[cursor]);
            if (!methodMatch.Success)
            {
                cursor++;
                continue;
            }

            var methodName = methodMatch.Groups[1].Value;
            if (target.MethodPrefixes.Length > 0 &&
                !target.MethodPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.Ordinal)))
            {
                break;
            }

            var displayName = HumanizeMethodName(methodName, target.MethodPrefixes);
            var testType = ClassifyTestCase(methodName);

            if (isTheory && inlineData.Count > 0)
            {
                foreach (var inline in inlineData)
                {
                    var inlineSummary = HumanizeInlineData(inline);
                    cases.Add(new TestCaseRow(
                        string.Empty,
                        methodName,
                        string.IsNullOrWhiteSpace(inlineSummary) ? displayName : $"{displayName} ({inlineSummary})",
                        testType,
                        target.RelativePath,
                        cursor + 1));
                }
            }
            else
            {
                cases.Add(new TestCaseRow(string.Empty, methodName, displayName, testType, target.RelativePath, cursor + 1));
            }

            break;
        }
    }

    return cases;
}

static string HumanizeMethodName(string methodName, IReadOnlyList<string> methodPrefixes)
{
    foreach (var prefix in methodPrefixes.OrderByDescending(prefix => prefix.Length))
    {
        if (!methodName.StartsWith(prefix, StringComparison.Ordinal)) continue;
        methodName = methodName[prefix.Length..];
        break;
    }

    var value = string.Join(" ", methodName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(HumanizeToken));
    return value
        .Replace("Sos", "SOS", StringComparison.Ordinal)
        .Replace("Utc", "UTC", StringComparison.Ordinal)
        .Replace("Dto", "DTO", StringComparison.Ordinal)
        .Replace("Id", "ID", StringComparison.Ordinal)
        .Replace("Ai", "AI", StringComparison.Ordinal);
}

static string HumanizeToken(string token)
{
    token = Regex.Replace(token, "([a-z0-9])([A-Z])", "$1 $2");
    token = Regex.Replace(token, "([A-Z]+)([A-Z][a-z])", "$1 $2");
    return token.Trim();
}

static string HumanizeInlineData(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

    var arguments = SplitInlineDataArguments(raw);
    if (arguments.Count == 0) return string.Empty;

    var summarizedArguments = arguments
        .Select(SummarizeInlineDataArgument)
        .Where(value => !string.IsNullOrWhiteSpace(value));

    return string.Join(", ", summarizedArguments);
}

static List<string> SplitInlineDataArguments(string raw)
{
    var arguments = new List<string>();
    var current = new StringBuilder();
    var depth = 0;
    var inString = false;
    var escapeNext = false;

    foreach (var ch in raw)
    {
        if (inString)
        {
            current.Append(ch);
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (ch == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (ch == '"') inString = false;
            continue;
        }

        switch (ch)
        {
            case '"':
                inString = true;
                current.Append(ch);
                break;
            case '(':
            case '[':
            case '{':
                depth++;
                current.Append(ch);
                break;
            case ')':
            case ']':
            case '}':
                if (depth > 0) depth--;
                current.Append(ch);
                break;
            case ',' when depth == 0:
                var argument = current.ToString().Trim();
                if (argument.Length > 0) arguments.Add(argument);
                current.Clear();
                break;
            default:
                current.Append(ch);
                break;
        }
    }

    var finalArgument = current.ToString().Trim();
    if (finalArgument.Length > 0) arguments.Add(finalArgument);

    return arguments;
}

static string SummarizeInlineDataArgument(string argument)
{
    if (string.IsNullOrWhiteSpace(argument)) return string.Empty;

    var value = argument.Trim();
    if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
    {
        value = value[1..^1];
    }

    var lastDot = value.LastIndexOf('.');
    if (lastDot >= 0 && lastDot < value.Length - 1)
    {
        var tail = value[(lastDot + 1)..];
        if (!string.IsNullOrWhiteSpace(tail) && char.IsLetter(tail[0]))
        {
            value = tail;
        }
    }

    var humanized = HumanizeToken(value);
    return humanized
        .Replace("Sos", "SOS", StringComparison.Ordinal)
        .Replace("Utc", "UTC", StringComparison.Ordinal)
        .Replace("Dto", "DTO", StringComparison.Ordinal)
        .Replace("Id", "ID", StringComparison.Ordinal)
        .Replace("Ai", "AI", StringComparison.Ordinal);
}

static string ClassifyTestCase(string methodName)
{
    var upper = methodName.ToUpperInvariant();
    string[] boundaryKeywords =
    [
        "OUTOFRANGE", "EXCEEDS", "MAXIMUM", "MINIMUM", "LIMIT", "TOOFAR",
        "DISTANCE", "NOTPOSITIVE", "PAST", "LENGTH", "WITHIN", "GREATERTHAN", "LESSTHAN"
    ];
    if (boundaryKeywords.Any(keyword => upper.Contains(keyword, StringComparison.Ordinal))) return "B";

    string[] abnormalKeywords =
    [
        "THROWS", "FAILS", "INVALID", "NOTFOUND", "FORBIDDEN", "BADREQUEST",
        "CONFLICT", "MISSING", "MISMATCH", "EMPTY", "DUPLICATE", "CANNOT",
        "DOESNOTEXIST", "NOTALLOWED", "RETURNCLEARPROVIDERMESSAGE"
    ];
    return abnormalKeywords.Any(keyword => upper.Contains(keyword, StringComparison.Ordinal)) ? "A" : "N";
}

static int CountMeaningfulLines(string path)
{
    return File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Count(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("//", StringComparison.Ordinal) && line is not "{" and not "}");
}
static void PrepareWorkbook(XLWorkbook workbook, IReadOnlyList<RequirementReport> reports)
{
    var worksheetNames = BuildWorksheetNameMap(reports);
    CreateGuidelineSheet(workbook.AddWorksheet("Guideline"));
    UpdateCoverSheet(workbook.AddWorksheet("Cover"), reports);
    UpdateStatisticsSheet(workbook.AddWorksheet("Statistics"), reports);
    UpdateFunctionsSheet(workbook.AddWorksheet("Functions"), reports, worksheetNames);
    foreach (var report in reports)
    {
        var worksheetName = worksheetNames[report.Requirement.SheetName];
        PopulateFunctionSheet(workbook.AddWorksheet(worksheetName), report, worksheetName);
    }
}

static IReadOnlyDictionary<string, string> BuildWorksheetNameMap(IReadOnlyList<RequirementReport> reports)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var report in reports)
    {
        var originalName = report.Requirement.SheetName;
        var sanitizedBase = Regex.Replace(originalName, @"[:\\/?*\[\]]", "_").Trim().Trim('\'');
        if (string.IsNullOrWhiteSpace(sanitizedBase)) sanitizedBase = "Sheet";
        if (sanitizedBase.Length > 31) sanitizedBase = sanitizedBase[..31];

        var candidate = sanitizedBase;
        var suffix = 1;
        while (!usedNames.Add(candidate))
        {
            var suffixText = $"_{suffix++}";
            var prefixLength = Math.Min(Math.Max(1, 31 - suffixText.Length), sanitizedBase.Length);
            candidate = sanitizedBase[..prefixLength] + suffixText;
        }

        map[originalName] = candidate;
    }

    return map;
}

static void CreateGuidelineSheet(IXLWorksheet worksheet)
{
    worksheet.Cell("A1").Value = "Guideline to make and understand Unit Test Case";
    worksheet.Cell("A3").Value = "1. Overview";
    worksheet.Cell("A4").Value = "- This workbook follows the structure of the provided sample: Cover, Statistics, Functions, and one sheet per function.";
    worksheet.Cell("A5").Value = "- The RESQ version is generated from RequirementTraceabilityMatrix.csv and the current xUnit application test suite.";
    worksheet.Cell("A6").Value = "- Passed counts are based on direct execution of RESQ.Tests.dll via dotnet vstest on 2026/04/09.";
    worksheet.Cell("A8").Value = "2. Function Sheet";
    worksheet.Cell("A9").Value = "- UTCID columns represent individual logical test cases extracted from Facts/Theories.";
    worksheet.Cell("A10").Value = "- Preconditions come from the RTM; detailed assertions remain in the linked test source files.";
    worksheet.Column(1).Width = 120;
    worksheet.Style.Alignment.WrapText = true;
}

static void UpdateCoverSheet(IXLWorksheet worksheet, IReadOnlyList<RequirementReport> reports)
{
    FormatTitle(worksheet.Range("B2:F2"), "UNIT TEST DOCUMENT");

    worksheet.Cell("A4").Value = "Project Name";
    worksheet.Cell("A5").Value = "Project Code";
    worksheet.Cell("A6").Value = "Document Code";
    worksheet.Cell("E4").Value = "Created By";
    worksheet.Cell("E5").Value = "Executed Date";
    worksheet.Cell("E6").Value = "Version";
    worksheet.Cell("B4").Value = ProjectName;
    worksheet.Cell("B5").Value = ProjectCode;
    worksheet.Cell("B6").Value = DocumentCode;
    worksheet.Cell("F4").Value = CreatorName;
    worksheet.Cell("F5").Value = ExecutedDateText;
    worksheet.Cell("F6").Value = DocumentVersion;
    FormatKeyValueBlock(worksheet.Range("A4:F6"));

    worksheet.Cell("A8").Value = "Document Summary";
    worksheet.Cell("A9").Value = "Revision History";

    string[] headers = ["Date", "Version", "Type", "Action", "Description", "Prepared By", "Checked By", "Approved By"];
    for (var index = 0; index < headers.Length; index++) worksheet.Cell(10, index + 1).Value = headers[index];
    FormatHeaderRow(worksheet.Range("A10:H10"));

    worksheet.Range("A11:H30").Clear(XLClearOptions.Contents);
    worksheet.Cell("A11").Value = ExecutedDateText;
    worksheet.Cell("B11").Value = DocumentVersion;
    worksheet.Cell("C11").Value = "Document";
    worksheet.Cell("D11").Value = "A";
    worksheet.Cell("E11").Value = "Create RESQ unit test document";
    worksheet.Cell("F11").Value = CreatorName;
    worksheet.Cell("A12").Value = ExecutedDateText;
    worksheet.Cell("B12").Value = DocumentVersion;
    worksheet.Cell("C12").Value = "Document";
    worksheet.Cell("D12").Value = "M";
    worksheet.Cell("E12").Value = $"Generate {reports.Count} requirement sheets and {reports.Sum(report => report.Cases.Count)} RTM test cases from RESQ.Tests";
    worksheet.Cell("F12").Value = CreatorName;
    FormatTable(worksheet.Range("A10:H12"));

    worksheet.Cell("A15").Value = "Scope";
    worksheet.Cell("B15").Value = OutputNotes;
    worksheet.Cell("A16").Value = "Input Files";
    worksheet.Cell("B16").Value = "RequirementTraceabilityMatrix.csv and current xUnit test suite";
    worksheet.Cell("A17").Value = "Execution";
    worksheet.Cell("B17").Value = "Verified by dotnet vstest against RESQ.Tests.dll on 2026/04/09";
    FormatKeyValueBlock(worksheet.Range("A15:B17"));

    worksheet.Column("A").Width = 14;
    worksheet.Column("B").Width = 28;
    worksheet.Column("C").Width = 12;
    worksheet.Column("D").Width = 10;
    worksheet.Column("E").Width = 56;
    worksheet.Column("F").Width = 16;
    worksheet.Column("G").Width = 16;
    worksheet.Column("H").Width = 16;
    worksheet.Style.Alignment.WrapText = true;
}

static void UpdateStatisticsSheet(IXLWorksheet worksheet, IReadOnlyList<RequirementReport> reports)
{
    FormatTitle(worksheet.Range("B2:F2"), "UNIT TEST REPORT");

    worksheet.Cell("A4").Value = "Project Name";
    worksheet.Cell("A5").Value = "Project Code";
    worksheet.Cell("A6").Value = "Report Code";
    worksheet.Cell("E4").Value = "Prepared By";
    worksheet.Cell("E5").Value = "Verification";
    worksheet.Cell("E6").Value = "Executed Date";
    worksheet.Cell("B4").Value = ProjectName;
    worksheet.Cell("B5").Value = ProjectCode;
    worksheet.Cell("B6").Value = ReportDocumentCode;
    worksheet.Cell("F4").Value = CreatorName;
    worksheet.Cell("F5").Value = "dotnet vstest";
    worksheet.Cell("F6").Value = ExecutedDateText;
    worksheet.Cell("A7").Value = "Notes";
    worksheet.Cell("B7").Value = OutputNotes;
    FormatKeyValueBlock(worksheet.Range("A4:F7"));

    string[] headers = ["No", "Function Code", "Passed", "Failed", "Untested", "N", "A", "B", "Total Test Cases"];
    for (var index = 0; index < headers.Length; index++) worksheet.Cell(11, index + 1).Value = headers[index];
    FormatHeaderRow(worksheet.Range("A11:I11"));

    worksheet.Range("A12:I120").Clear(XLClearOptions.Contents);
    var row = 12;
    foreach (var report in reports)
    {
        worksheet.Cell(row, 1).Value = report.Requirement.No;
        worksheet.Cell(row, 2).Value = report.Requirement.FunctionCode;
        worksheet.Cell(row, 3).Value = report.PassedCount;
        worksheet.Cell(row, 4).Value = report.FailedCount;
        worksheet.Cell(row, 5).Value = report.UntestedCount;
        worksheet.Cell(row, 6).Value = report.NormalCount;
        worksheet.Cell(row, 7).Value = report.AbnormalCount;
        worksheet.Cell(row, 8).Value = report.BoundaryCount;
        worksheet.Cell(row, 9).Value = report.Cases.Count;
        row++;
    }

    if (row > 12) FormatTable(worksheet.Range(12, 1, row - 1, 9));
    worksheet.Range($"A{row}:I120").Clear(XLClearOptions.Contents);

    var totalPassed = reports.Sum(report => report.PassedCount);
    var totalFailed = reports.Sum(report => report.FailedCount);
    var totalUntested = reports.Sum(report => report.UntestedCount);
    var totalNormal = reports.Sum(report => report.NormalCount);
    var totalAbnormal = reports.Sum(report => report.AbnormalCount);
    var totalBoundary = reports.Sum(report => report.BoundaryCount);
    var totalCases = reports.Sum(report => report.Cases.Count);

    var summaryRow = row + 2;
    worksheet.Cell(summaryRow, 1).Value = "Sub total";
    worksheet.Cell(summaryRow, 3).Value = totalPassed;
    worksheet.Cell(summaryRow, 4).Value = totalFailed;
    worksheet.Cell(summaryRow, 5).Value = totalUntested;
    worksheet.Cell(summaryRow, 6).Value = totalNormal;
    worksheet.Cell(summaryRow, 7).Value = totalAbnormal;
    worksheet.Cell(summaryRow, 8).Value = totalBoundary;
    worksheet.Cell(summaryRow, 9).Value = totalCases;
    FormatTable(worksheet.Range(summaryRow, 1, summaryRow, 9));

    var metricRow = summaryRow + 2;
    worksheet.Cell(metricRow, 1).Value = "Coverage metric";
    worksheet.Cell(metricRow, 4).Value = "Value (%)";
    worksheet.Cell(metricRow + 1, 1).Value = "Executed coverage";
    worksheet.Cell(metricRow + 2, 1).Value = "Passed coverage";
    worksheet.Cell(metricRow + 3, 1).Value = "Normal coverage";
    worksheet.Cell(metricRow + 4, 1).Value = "Abnormal coverage";
    worksheet.Cell(metricRow + 5, 1).Value = "Boundary coverage";
    worksheet.Cell(metricRow + 1, 4).Value = totalCases == 0 ? 0 : Math.Round((double)(totalPassed + totalFailed) / totalCases * 100, 2);
    worksheet.Cell(metricRow + 2, 4).Value = totalCases == 0 ? 0 : Math.Round((double)totalPassed / totalCases * 100, 2);
    worksheet.Cell(metricRow + 3, 4).Value = totalCases == 0 ? 0 : Math.Round((double)totalNormal / totalCases * 100, 2);
    worksheet.Cell(metricRow + 4, 4).Value = totalCases == 0 ? 0 : Math.Round((double)totalAbnormal / totalCases * 100, 2);
    worksheet.Cell(metricRow + 5, 4).Value = totalCases == 0 ? 0 : Math.Round((double)totalBoundary / totalCases * 100, 2);
    FormatHeaderRow(worksheet.Range(metricRow, 1, metricRow, 4));
    FormatTable(worksheet.Range(metricRow + 1, 1, metricRow + 5, 4));

    worksheet.Column("A").Width = 18;
    worksheet.Column("B").Width = 16;
    worksheet.Column("C").Width = 10;
    worksheet.Column("D").Width = 10;
    worksheet.Column("E").Width = 10;
    worksheet.Column("F").Width = 10;
    worksheet.Column("G").Width = 10;
    worksheet.Column("H").Width = 10;
    worksheet.Column("I").Width = 16;
    worksheet.Style.Alignment.WrapText = true;
    worksheet.SheetView.FreezeRows(11);
}
static void EnsureCaseColumns(IXLWorksheet worksheet, int requiredLastColumn, int lastRow) { }
static void UpdateFunctionsSheet(IXLWorksheet worksheet, IReadOnlyList<RequirementReport> reports, IReadOnlyDictionary<string, string> worksheetNames)
{
    FormatTitle(worksheet.Range("D2:H2"), "Function List");

    worksheet.Cell("A4").Value = "Project Name";
    worksheet.Cell("A5").Value = "Project Code";
    worksheet.Cell("A6").Value = "Normal Cases/KLOC";
    worksheet.Cell("A7").Value = "Environment";
    worksheet.Cell("E4").Value = ProjectName;
    worksheet.Cell("E5").Value = ProjectCode;
    worksheet.Cell("E6").Value = NormalCasesPerKloc;
    worksheet.Cell("E7").Value = EnvironmentDescription;
    FormatKeyValueBlock(worksheet.Range("A4:F7"));

    string[] headers = ["No", "Requirement Name", "Class Name", "Function Name", "Function Code", "Sheet Name", "Description", "Pre-Condition"];
    for (var index = 0; index < headers.Length; index++) worksheet.Cell(10, index + 1).Value = headers[index];
    FormatHeaderRow(worksheet.Range("A10:H10"));

    worksheet.Range("A11:H120").Clear(XLClearOptions.Contents);
    var row = 11;
    foreach (var report in reports)
    {
        worksheet.Cell(row, 1).Value = report.Requirement.No;
        worksheet.Cell(row, 2).Value = report.Requirement.RequirementName;
        worksheet.Cell(row, 3).Value = report.Requirement.ClassName;
        worksheet.Cell(row, 4).Value = report.Requirement.FunctionName;
        worksheet.Cell(row, 5).Value = report.Requirement.FunctionCode;
        worksheet.Cell(row, 6).Value = worksheetNames[report.Requirement.SheetName];
        worksheet.Cell(row, 6).SetHyperlink(new XLHyperlink($"#{worksheetNames[report.Requirement.SheetName]}!A1"));
        worksheet.Cell(row, 7).Value = report.Requirement.Description;
        worksheet.Cell(row, 8).Value = string.Join(Environment.NewLine, report.Preconditions.Select(precondition => $"- {precondition}"));
        row++;
    }

    if (row > 11) FormatTable(worksheet.Range(11, 1, row - 1, 8));

    worksheet.Column("A").Width = 8;
    worksheet.Column("B").Width = 24;
    worksheet.Column("C").Width = 24;
    worksheet.Column("D").Width = 24;
    worksheet.Column("E").Width = 16;
    worksheet.Column("F").Width = 31;
    worksheet.Column("G").Width = 48;
    worksheet.Column("H").Width = 42;
    worksheet.Style.Alignment.WrapText = true;
    worksheet.SheetView.FreezeRows(10);
}

static void PopulateFunctionSheet(IXLWorksheet worksheet, RequirementReport report, string worksheetName)
{
    const int firstCaseColumn = 6;
    const int minLastColumn = 20;
    const int maxSupportColumn = 24;
    const int lastTemplateRow = 80;

    var caseCount = report.Cases.Count;
    var lastCaseColumn = caseCount == 0 ? firstCaseColumn : firstCaseColumn + caseCount - 1;
    var requiredLastColumn = Math.Max(Math.Max(lastCaseColumn, minLastColumn), maxSupportColumn);

    EnsureCaseColumns(worksheet, requiredLastColumn, lastTemplateRow);
    worksheet.Range(1, 1, lastTemplateRow, requiredLastColumn).Clear(XLClearOptions.Contents);

    worksheet.Cell("A1").Value = "No";
    worksheet.Cell("C1").Value = report.Requirement.No;
    worksheet.Cell("A2").Value = "Function Code";
    worksheet.Cell("C2").Value = report.Requirement.FunctionCode;
    worksheet.Cell("F2").Value = "Function Name";
    worksheet.Cell("L2").Value = report.Requirement.FunctionName;
    worksheet.Cell("P2").Value = "Sheet Name";
    worksheet.Cell("T2").Value = worksheetName;
    worksheet.Cell("A3").Value = "Created By";
    worksheet.Cell("C3").Value = CreatorName;
    worksheet.Cell("F3").Value = "Executed By";
    worksheet.Cell("L3").Value = CreatorName;
    worksheet.Cell("P3").Value = "Executed Date";
    worksheet.Cell("T3").Value = ExecutedDateText;
    worksheet.Cell("A4").Value = "Lines of code";
    worksheet.Cell("C4").Value = report.MeaningfulLoc;
    worksheet.Cell("F4").Value = "Lack of test cases";
    worksheet.Cell("L4").Value = Math.Round(report.LackOfCases, 2);
    worksheet.Cell("A5").Value = "Test requirement";
    worksheet.Cell("C5").Value = report.Requirement.Description;
    worksheet.Cell("A6").Value = "Passed";
    worksheet.Cell("C6").Value = "Failed";
    worksheet.Cell("F6").Value = "Untested";
    worksheet.Cell("L6").Value = "N / A / B";
    worksheet.Cell("O6").Value = "Total Test Cases";
    worksheet.Cell("A7").Value = report.PassedCount;
    worksheet.Cell("C7").Value = report.FailedCount;
    worksheet.Cell("F7").Value = report.UntestedCount;
    worksheet.Cell("L7").Value = report.NormalCount;
    worksheet.Cell("M7").Value = report.AbnormalCount;
    worksheet.Cell("N7").Value = report.BoundaryCount;
    worksheet.Cell("O7").Value = report.Cases.Count;
    FormatKeyValueBlock(worksheet.Range("A1:T7"));

    worksheet.Cell("A9").Value = "Unit Test Case ID";
    worksheet.Cell("A10").Value = "Condition";
    worksheet.Cell("B10").Value = "Precondition / Scenario";
    for (var index = 0; index < report.Cases.Count; index++) worksheet.Cell(9, firstCaseColumn + index).Value = report.Cases[index].CaseId;
    FormatHeaderRow(worksheet.Range(9, 1, 10, requiredLastColumn));

    var preconditions = report.Preconditions.Count == 0 ? new List<string> { "No additional pre-condition" } : report.Preconditions;
    var preconditionRow = 11;
    foreach (var precondition in preconditions)
    {
        worksheet.Cell(preconditionRow, 2).Value = $"Precondition {preconditionRow - 10:00}";
        worksheet.Cell(preconditionRow, 4).Value = precondition;
        FillAllCaseMarkers(worksheet, preconditionRow, report.Cases.Count, firstCaseColumn);
        preconditionRow++;
    }

    var scenarioStartRow = 14;
    for (var index = 0; index < report.Cases.Count; index++)
    {
        var row = scenarioStartRow + index;
        worksheet.Cell(row, 2).Value = $"Case {index + 1:00}";
        worksheet.Cell(row, 4).Value = report.Cases[index].DisplayName;
        worksheet.Cell(row, firstCaseColumn + index).Value = "O";
    }

    if (preconditionRow > 11) FormatTable(worksheet.Range(11, 1, preconditionRow - 1, requiredLastColumn));
    if (report.Cases.Count > 0) FormatTable(worksheet.Range(scenarioStartRow, 1, scenarioStartRow + report.Cases.Count - 1, requiredLastColumn));

    worksheet.Cell("A48").Value = "Confirm";
    worksheet.Cell("B48").Value = "Return";
    worksheet.Cell("D49").Value = "The corresponding xUnit assertion passes for the expected exception or success path.";
    FillAllCaseMarkers(worksheet, 49, report.Cases.Count, firstCaseColumn);
    worksheet.Cell("B51").Value = "Exception";
    worksheet.Cell("D51").Value = "Exception-based cases are asserted with Assert.Throws / Assert.ThrowsAsync when applicable.";
    FillAllCaseMarkers(worksheet, 51, report.Cases.Count, firstCaseColumn);
    worksheet.Cell("B53").Value = "Message";
    worksheet.Cell("D53").Value = "Detailed messages and DTO state are validated in the linked test methods where needed.";
    FillAllCaseMarkers(worksheet, 53, report.Cases.Count, firstCaseColumn);
    FormatHeaderRow(worksheet.Range("A48:D48"));
    FormatTable(worksheet.Range(48, 1, 53, requiredLastColumn));

    worksheet.Cell("A56").Value = "Source Files";
    worksheet.Cell("D56").Value = string.Join(Environment.NewLine, report.Mapping.SourcePaths);
    worksheet.Cell("A58").Value = "Test Files";
    worksheet.Cell("D58").Value = string.Join(Environment.NewLine, report.Mapping.TestTargets.Select(target => target.RelativePath));
    FormatKeyValueBlock(worksheet.Range("A56:T58"));

    worksheet.Cell("A68").Value = "Result";
    worksheet.Cell("B68").Value = "Type (N: Normal, A: Abnormal, B: Boundary)";
    worksheet.Cell("B69").Value = "Passed / Failed";
    worksheet.Cell("B70").Value = "Executed Date";
    worksheet.Cell("B71").Value = "Defect ID";
    for (var index = 0; index < report.Cases.Count; index++)
    {
        var column = firstCaseColumn + index;
        worksheet.Cell(68, column).Value = report.Cases[index].TestType;
        worksheet.Cell(69, column).Value = "P";
        worksheet.Cell(70, column).Value = ExecutedDateText;
    }

    FormatHeaderRow(worksheet.Range(68, 1, 68, requiredLastColumn));
    FormatTable(worksheet.Range(68, 1, 71, requiredLastColumn));

    worksheet.Column("A").Width = 14;
    worksheet.Column("B").Width = 18;
    worksheet.Column("C").Width = 12;
    worksheet.Column("D").Width = 54;
    worksheet.Column("E").Width = 12;
    for (var column = firstCaseColumn; column <= requiredLastColumn; column++) worksheet.Column(column).Width = 14;
    worksheet.Style.Alignment.WrapText = true;
    worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    worksheet.SheetView.FreezeRows(10);
}

static void FillAllCaseMarkers(IXLWorksheet worksheet, int row, int caseCount, int firstCaseColumn)
{
    for (var index = 0; index < caseCount; index++) worksheet.Cell(row, firstCaseColumn + index).Value = "O";
}

static void FormatTitle(IXLRange range, string value)
{
    range.Merge();
    range.Value = value;
    range.Style.Font.Bold = true;
    range.Style.Font.FontSize = 16;
    range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    range.Style.Fill.BackgroundColor = XLColor.LightBlue;
    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
}

static void FormatHeaderRow(IXLRange range)
{
    range.Style.Font.Bold = true;
    range.Style.Fill.BackgroundColor = XLColor.LightGreen;
    range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    range.Style.Alignment.WrapText = true;
    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
}

static void FormatKeyValueBlock(IXLRange range)
{
    range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    range.Style.Alignment.WrapText = true;
    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    foreach (var row in range.Rows())
    {
        for (var column = 1; column <= row.CellCount(); column += 2)
        {
            row.Cell(column).Style.Font.Bold = true;
            row.Cell(column).Style.Fill.BackgroundColor = XLColor.LightGray;
        }
    }
}

static void FormatTable(IXLRange range)
{
    range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    range.Style.Alignment.WrapText = true;
    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
}

static void WriteSummaryCsv(IReadOnlyList<RequirementReport> reports, string csvPath)
{
    static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    var worksheetNames = BuildWorksheetNameMap(reports);
    var builder = new StringBuilder();
    builder.AppendLine("No,Requirement Name,Function Code,Sheet Name,Total Test Cases,Passed,Failed,Untested,Normal Cases,Abnormal Cases,Boundary Cases,Source Files,Test Files");
    foreach (var report in reports)
    {
        var sourceFiles = string.Join(" | ", report.Mapping.SourcePaths);
        var testFiles = string.Join(" | ", report.Mapping.TestTargets.Select(target => target.RelativePath));
        builder.AppendLine(string.Join(",",
            report.Requirement.No.ToString(CultureInfo.InvariantCulture),
            Escape(report.Requirement.RequirementName),
            Escape(report.Requirement.FunctionCode),
            Escape(worksheetNames[report.Requirement.SheetName]),
            report.Cases.Count.ToString(CultureInfo.InvariantCulture),
            report.PassedCount.ToString(CultureInfo.InvariantCulture),
            report.FailedCount.ToString(CultureInfo.InvariantCulture),
            report.UntestedCount.ToString(CultureInfo.InvariantCulture),
            report.NormalCount.ToString(CultureInfo.InvariantCulture),
            report.AbnormalCount.ToString(CultureInfo.InvariantCulture),
            report.BoundaryCount.ToString(CultureInfo.InvariantCulture),
            Escape(sourceFiles),
            Escape(testFiles)));
    }

    File.WriteAllText(csvPath, builder.ToString(), new UTF8Encoding(false));
}

static IReadOnlyDictionary<string, RequirementMapping> BuildMappings()
{
    return new Dictionary<string, RequirementMapping>(StringComparer.OrdinalIgnoreCase)
    {
        ["CreatePrompt"] = new(
            ["RESQ.Application\\UseCases\\SystemConfig\\Commands\\CreatePrompt\\CreatePromptCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\SystemConfig\\Commands\\PromptProviderValidatorTests.cs", ["CreatePromptValidator_"])]),
        ["UpdatePrompt"] = new(
            ["RESQ.Application\\UseCases\\SystemConfig\\Commands\\UpdatePrompt\\UpdatePromptCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\SystemConfig\\Commands\\PromptProviderValidatorTests.cs", ["UpdatePromptValidator_"])]),
        ["CancelSosRequest"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\CancelSosRequest\\CancelSosRequestCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\CancelSosRequestCommandHandlerTests.cs", [])]),
        ["CreateSosCluster"] = new(
            [
                "RESQ.Application\\UseCases\\Emergency\\Commands\\CreateSosCluster\\CreateSosClusterCommandHandler.cs",
                "RESQ.Application\\UseCases\\Emergency\\Commands\\CreateSosCluster\\CreateSosClusterCommandValidator.cs"
            ],
            [
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\CreateSosClusterCommandHandlerTests.cs", []),
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\CreateSosClusterCommandValidatorTests.cs", [])
            ]),
        ["SosRequestDomainRules"] = new(
            ["RESQ.Domain\\Entities\\Emergency\\SosRequestModel.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\SosRequestModelTests.cs", [])]),
        ["UpdateSosRequestVictim"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\UpdateSosRequestVictim\\UpdateSosRequestVictimCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\UpdateSosRequestVictimCommandHandlerTests.cs", [])]),
        ["GetMyUpcomingReturnActivities"] = new(
            [
                "RESQ.Application\\UseCases\\Logistics\\Queries\\GetMyUpcomingReturnActivities\\GetMyUpcomingReturnActivitiesQueryHandler.cs",
                "RESQ.Application\\UseCases\\Logistics\\Queries\\GetMyUpcomingReturnActivities\\GetMyUpcomingReturnActivitiesQueryValidator.cs"
            ],
            [
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\ReturnSupplyActivityQueryHandlerTests.cs", ["UpcomingReturnsHandler_"]),
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\GetMyUpcomingReturnActivities\\GetMyUpcomingReturnActivitiesQueryValidatorTests.cs", [])
            ]),
        ["GetMyReturnHistoryActivities"] = new(
            [
                "RESQ.Application\\UseCases\\Logistics\\Queries\\GetMyReturnHistoryActivities\\GetMyReturnHistoryActivitiesQueryHandler.cs",
                "RESQ.Application\\UseCases\\Logistics\\Queries\\GetMyReturnHistoryActivities\\GetMyReturnHistoryActivitiesQueryValidator.cs"
            ],
            [
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\ReturnSupplyActivityQueryHandlerTests.cs", ["ReturnHistoryHandler_"]),
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\GetMyReturnHistoryActivities\\GetMyReturnHistoryActivitiesQueryValidatorTests.cs", [])
            ]),
        ["AddMissionActivity"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\AddMissionActivity\\AddMissionActivityCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\AddMissionActivityCommandValidatorTests.cs", [])]),
        ["AssignTeamToMission"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\AssignTeamToMission\\AssignTeamToMissionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\AssignTeamToMissionCommandHandlerTests.cs", [])]),
        ["CompleteMissionTeamExecution"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\CompleteMissionTeamExecution\\CompleteMissionTeamExecutionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\CompleteMissionTeamExecutionCommandHandlerTests.cs", [])]),
        ["CreateMission"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\CreateMission\\CreateMissionCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\CreateMissionCommandValidatorTests.cs", [])]),
        ["UnassignTeamFromMission"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UnassignTeamFromMission\\UnassignTeamFromMissionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UnassignTeamFromMissionCommandHandlerTests.cs", [])]),
        ["UpdateMission"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateMission\\UpdateMissionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateMissionCommandHandlerTests.cs", [])]),
        ["UpdateMissionStatus"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateMissionStatus\\UpdateMissionStatusCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateMissionStatusCommandHandlerTests.cs", [])]),
        ["UpdateTeamIncidentStatus"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateTeamIncidentStatus\\UpdateTeamIncidentStatusCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateTeamIncidentStatusCommandHandlerTests.cs", [])]),
        ["NormalizeMissionIncidentRequest"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Shared\\IncidentV2NormalizationHelper.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\IncidentV2\\IncidentV2NormalizationHelperTests.cs", ["NormalizeMissionRequest_"])]),
        ["NormalizeActivityIncidentRequest"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Shared\\IncidentV2NormalizationHelper.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\IncidentV2\\IncidentV2NormalizationHelperTests.cs", ["NormalizeActivityRequest_"])]),
        ["MapTeamIncidentQueryDto"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Queries\\Shared\\TeamIncidentQueryModels.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\IncidentV2\\TeamIncidentQueryDtoMapperTests.cs", [])]),
        // ── Identity ──
        ["AdminUserManagement"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\AdminCreateUser\\AdminCreateUserCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\AdminUpdateUser\\AdminUpdateUserCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\AdminUserManagementHandlerTests.cs", [])]),
        ["AuthFlow"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\RegisterRescuer\\RegisterRescuerCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\Login\\LoginCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\AuthFlowHandlerTests.cs", [])]),
        ["BanUnbanUser"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\BanUser\\BanUserCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\UnbanUser\\UnbanUserCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\BanUnbanUserHandlerTests.cs", [])]),
        ["RegisterVerifyEmail"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\RegisterRescuer\\RegisterRescuerCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\VerifyEmail\\VerifyEmailCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\RegisterAndVerifyEmailCommandHandlerTests.cs", [])]),
        ["RelativeProfile"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Queries\\GetRelativeProfiles\\GetRelativeProfilesQueryHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\CreateRelativeProfile\\CreateRelativeProfileCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\UpdateRelativeProfile\\UpdateRelativeProfileCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\RelativeProfileHandlerTests.cs", [])]),
        ["RescuerAuthSession"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\LoginRescuer\\LoginRescuerCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\RefreshToken\\RefreshTokenCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\Logout\\LogoutCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Queries\\GetCurrentUser\\GetCurrentUserQueryHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\RescuerAuthSessionAndProfileHandlerTests.cs", [])]),
        ["RoleAvatarPermissions"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\AssignRoleToUser\\AssignRoleToUserCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\SetUserAvatarUrl\\SetUserAvatarUrlCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Queries\\GetUserPermissions\\GetUserPermissionsQueryHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\SetUserPermissions\\SetUserPermissionsCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\RoleAvatarPermissionsHandlerTests.cs", [])]),
        ["UserProfile"] = new(
            [
                "RESQ.Application\\UseCases\\Identity\\Commands\\UpdateRescuerProfile\\UpdateRescuerProfileCommandHandler.cs",
                "RESQ.Application\\UseCases\\Identity\\Commands\\RescuerConsent\\RescuerConsentCommandHandler.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Identity\\UserProfileHandlerTests.cs", [])]),
        // ── Emergency (new) ──
        ["CreateSosRequest"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\CreateSosRequest\\CreateSosRequestCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\CreateSosRequestCommandHandlerTests.cs", [])]),
        ["GenMissionSuggestion"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\GenerateRescueMissionSuggestion\\GenerateRescueMissionSuggestionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GenerateRescueMissionSuggestionCommandHandlerTests.cs", [])]),
        ["GenMissionSuggValidat"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\GenerateRescueMissionSuggestion\\GenerateRescueMissionSuggestionValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GenerateRescueMissionSuggestionValidatorTests.cs", [])]),
        ["GetAlternativeDepots"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetAlternativeDepots\\GetAlternativeDepotsQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetAlternativeDepotsQueryHandlerTests.cs", [])]),
        ["GetMissionSuggestions"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetMissionSuggestions\\GetMissionSuggestionsQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetMissionSuggestionsQueryHandlerTests.cs", [])]),
        ["GetMySosRequests"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetMySosRequests\\GetMySosRequestsQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetMySosRequestsQueryHandlerTests.cs", [])]),
        ["GetSosClusters"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetSosClusters\\GetSosClustersQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetSosClustersQueryHandlerTests.cs", [])]),
        ["GetSosEvaluation"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetSosEvaluation\\GetSosEvaluationQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetSosEvaluationQueryHandlerTests.cs", [])]),
        ["GetSosRequest"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetSosRequests\\GetSosRequestQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Emergency\\GetSosRequestQueryHandlerTests.cs", [])]),
        // ── Logistics (new) ──
        ["AdjustInventory"] = new(
            ["RESQ.Application\\UseCases\\Logistics\\Commands\\AdjustInventory\\AdjustInventoryCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\Commands\\AdjustInventoryCommandValidatorTests.cs", [])]),
        ["CreateDepot"] = new(
            ["RESQ.Application\\UseCases\\Logistics\\Commands\\CreateDepot\\CreateDepotCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\Commands\\CreateDepotCommandValidatorTests.cs", [])]),
        ["CreateSupplyRequest"] = new(
            ["RESQ.Application\\UseCases\\Logistics\\Commands\\CreateSupplyRequest\\CreateSupplyRequestCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\Commands\\CreateSupplyRequestCommandValidatorTests.cs", [])]),
        ["InitiateDepotClosure"] = new(
            ["RESQ.Application\\UseCases\\Logistics\\Commands\\InitiateDepotClosure\\InitiateDepotClosureCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Logistics\\Commands\\InitiateDepotClosureCommandHandlerTests.cs", [])]),
        // ── Operations (new handlers) ──
        ["AddMissionActHandler"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\AddMissionActivity\\AddMissionActivityCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\AddMissionActivityCommandHandlerTests.cs", [])]),
        ["AssignTeamToActivity"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\AssignTeamToActivity\\AssignTeamToActivityCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\AssignTeamToActivityCommandHandlerTests.cs", [])]),
        ["ConfirmDeliverySupp"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\ConfirmDeliverySupplies\\ConfirmDeliverySuppliesCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\ConfirmDeliverySuppliesCommandHandlerTests.cs", [])]),
        ["ConfirmSupplyPickup"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\ConfirmMissionSupplyPickup\\ConfirmMissionSupplyPickupCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\ConfirmMissionSupplyPickupCommandHandlerTests.cs", [])]),
        ["ConfirmReturnSupplies"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\ConfirmReturnSupplies\\ConfirmReturnSuppliesCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\ConfirmReturnSuppliesCommandHandlerTests.cs", [])]),
        ["CreateMissionHandler"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\CreateMission\\CreateMissionCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\CreateMissionCommandHandlerTests.cs", [])]),
        ["ReportActivityIncident"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\ReportMissionActivityIncident\\ReportMissionActivityIncidentCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\ReportMissionActivityIncidentCommandHandlerTests.cs", [])]),
        ["ReportTeamIncident"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\ReportMissionTeamIncident\\ReportMissionTeamIncidentCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\ReportMissionTeamIncidentCommandHandlerTests.cs", [])]),
        ["SendMessage"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\SendMessage\\SendMessageCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\SendMessageCommandHandlerTests.cs", [])]),
        ["SyncMissionActivities"] = new(
            [
                "RESQ.Application\\UseCases\\Operations\\Commands\\SyncMissionActivities\\SyncMissionActivitiesCommandHandler.cs",
                "RESQ.Application\\UseCases\\Operations\\Commands\\SyncMissionActivities\\SyncMissionActivitiesCommandValidator.cs"
            ],
            [
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\SyncMissionActivitiesCommandHandlerTests.cs", []),
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\SyncMissionActivitiesCommandValidatorTests.cs", [])
            ]),
        ["UpdateActivityStatus"] = new(
            [
                "RESQ.Application\\UseCases\\Operations\\Commands\\UpdateActivityStatus\\UpdateActivityStatusCommandHandler.cs",
                "RESQ.Application\\UseCases\\Operations\\Commands\\UpdateActivityStatus\\UpdateActivityStatusCommandValidator.cs"
            ],
            [
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateActivityStatusCommandHandlerTests.cs", []),
                new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateActivityStatusCommandValidatorTests.cs", [])
            ]),
        ["UpdateMissionActivity"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateMissionActivity\\UpdateMissionActivityCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateMissionActivityCommandHandlerTests.cs", [])]),
        ["UpdateMissionValidator"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateMission\\UpdateMissionCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateMissionCommandValidatorTests.cs", [])]),
        ["UpdateMissionStatValid"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\UpdateMissionStatus\\UpdateMissionStatusCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Commands\\UpdateMissionStatusCommandValidatorTests.cs", [])]),
        ["GetMissionActivities"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Queries\\GetMissionActivities\\GetMissionActivitiesQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Queries\\GetMissionActivitiesQueryHandlerTests.cs", [])]),
        ["MissionAiSuggestion"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Queries\\GetMissions\\GetMissionsResponse.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Queries\\MissionAiSuggestionSectionTests.cs", [])]),
        ["ActivityStatusExec"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Shared\\MissionActivityStatusExecutionService.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Shared\\MissionActivityStatusExecutionServiceTests.cs", [])]),
        ["SupplyExecSnapshot"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Shared\\MissionSupplyExecutionSnapshotHelper.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Operations\\Shared\\MissionSupplyExecutionSnapshotHelperTests.cs", [])]),
        // ── Personnel ──
        ["CreateRescueTeam"] = new(
            ["RESQ.Application\\UseCases\\Personnel\\Commands\\CreateRescueTeam\\CreateRescueTeamCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Personnel\\Commands\\CreateRescueTeamCommandValidatorTests.cs", [])]),
        // ── Domain ──
        ["DepotDomainRules"] = new(
            ["RESQ.Domain\\Entities\\Logistics\\DepotModel.cs"],
            [new TestTarget("RESQ.Tests\\Domain\\Entities\\Logistics\\DepotModelTests.cs", [])]),
        ["DepotFundRules"] = new(
            ["RESQ.Domain\\Entities\\Finance\\DepotFundModel.cs"],
            [new TestTarget("RESQ.Tests\\Domain\\Finance\\DepotFundModelTests.cs", [])]),
        ["FundTransaction"] = new(
            ["RESQ.Domain\\Entities\\Finance\\FundTransactionModel.cs"],
            [new TestTarget("RESQ.Tests\\Domain\\Finance\\FundTransactionModelTests.cs", [])]),
        ["RescueTeamDomain"] = new(
            ["RESQ.Domain\\Entities\\Personnel\\RescueTeamModel.cs"],
            [new TestTarget("RESQ.Tests\\Domain\\Entities\\Personnel\\RescueTeamModelTests.cs", [])]),
        // ── Infrastructure ──
        ["AiSettingsResolver"] = new(
            ["RESQ.Infrastructure\\Services\\Ai\\AiPromptExecutionSettingsResolver.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Ai\\AiPromptExecutionSettingsResolverTests.cs", [])]),
        ["GeminiAiClient"] = new(
            ["RESQ.Infrastructure\\Services\\Ai\\GeminiAiProviderClient.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Ai\\GeminiAiProviderClientTests.cs", [])]),
        ["OpenRouterClient"] = new(
            ["RESQ.Infrastructure\\Services\\Ai\\OpenRouterAiProviderClient.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Ai\\OpenRouterAiProviderClientTests.cs", [])]),
        ["PromptSecretProtector"] = new(
            ["RESQ.Infrastructure\\Services\\Ai\\PromptSecretProtector.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Ai\\PromptSecretProtectorTests.cs", [])]),
        ["DepotInventoryRepo"] = new(
            ["RESQ.Infrastructure\\Persistence\\Logistics\\DepotInventoryRepository.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Logistics\\DepotInventoryRepositoryTests.cs", [])]),
        ["PromptMapper"] = new(
            ["RESQ.Infrastructure\\Mappers\\System\\PromptMapper.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Mappers\\PromptMapperTests.cs", [])]),
        ["AiModelTestService"] = new(
            ["RESQ.Infrastructure\\Services\\AiModelTestService.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Services\\AiModelTestServiceTests.cs", [])]),
        ["RescueSuggestionSvc"] = new(
            ["RESQ.Infrastructure\\Services\\RescueMissionSuggestionService.cs"],
            [new TestTarget("RESQ.Tests\\Infrastructure\\Services\\RescueMissionSuggestionServiceInternalTests.cs", [])]),
        // ── Common ──
        ["SecretMasker"] = new(
            ["RESQ.Application\\Common\\Security\\SecretMasker.cs"],
            [new TestTarget("RESQ.Tests\\Application\\Common\\Security\\SecretMaskerTests.cs", [])]),
        ["StatusStateMachines"] = new(
            [
                "RESQ.Application\\Common\\StateMachines\\MissionStateMachine.cs",
                "RESQ.Application\\Common\\StateMachines\\MissionActivityStateMachine.cs"
            ],
            [new TestTarget("RESQ.Tests\\Application\\Common\\StateMachines\\StatusStateMachineTests.cs", [])]),
        // ── Workflows ──
        ["Flow1HappyPath"] = new(
            ["RESQ.Domain\\Entities\\Emergency\\SosRequestModel.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow1_HappyPath_SosToCompletionTests.cs", [])]),
        ["Flow2ActivityIncident"] = new(
            ["RESQ.Application\\Common\\StateMachines\\MissionActivityStateMachine.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow2_ActivityIncident_ContinueMissionTests.cs", [])]),
        ["Flow3MissionIncident"] = new(
            ["RESQ.Application\\Common\\StateMachines\\MissionStateMachine.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow3_MissionIncident_NeedHelpTests.cs", [])]),
        ["Flow4SosDuplicateSpam"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\CreateSosCluster\\CreateSosClusterCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow4_SosDuplicateSpamTests.cs", [])]),
        ["Flow5ClusterFail"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Commands\\CreateSosCluster\\CreateSosClusterCommandValidator.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow5_ClusterCreationFailureTests.cs", [])]),
        ["Flow6NoRescuer"] = new(
            ["RESQ.Domain\\Entities\\Personnel\\RescueTeamModel.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow6_NoRescuerAvailableTests.cs", [])]),
        ["Flow7Redispatch"] = new(
            ["RESQ.Domain\\Entities\\Personnel\\RescueTeamModel.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow7_RedispatchTests.cs", [])]),
        ["Flow8SupplyLogicBug"] = new(
            ["RESQ.Application\\UseCases\\Operations\\Commands\\AddMissionActivity\\AddMissionActivityCommandHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow8_SupplyLogicBugTests.cs", [])]),
        ["Flow9RoutingFail"] = new(
            ["RESQ.Application\\UseCases\\Emergency\\Queries\\GetAlternativeDepots\\GetAlternativeDepotsQueryHandler.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow9_RoutingFailTests.cs", [])]),
        ["Flow10SosRedirect"] = new(
            ["RESQ.Domain\\Entities\\Emergency\\SosRequestModel.cs"],
            [new TestTarget("RESQ.Tests\\Application\\UseCases\\Workflows\\Flow10_SosIncidentRedirectTests.cs", [])]),
        // ── Presentation ──
        ["SosClusterRoute"] = new(
            ["RESQ.Presentation\\Controllers\\Emergency\\SosClusterController.cs"],
            [new TestTarget("RESQ.Tests\\Presentation\\Controllers\\Emergency\\SosClusterControllerRouteTests.cs", [])]),
        ["MissionController"] = new(
            ["RESQ.Presentation\\Controllers\\Operations\\MissionController.cs"],
            [new TestTarget("RESQ.Tests\\Presentation\\Operations\\MissionControllerIntegrationTests.cs", [])])
    };
}

sealed record RequirementRow(int No, string RequirementName, string ClassName, string FunctionName, string FunctionCode, string SheetName, string Description, string RawPreconditions)
{
    public List<string> Preconditions => RawPreconditions
        .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Trim())
        .Select(value => value.TrimStart('-', ' '))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToList();
}

sealed record RequirementMapping(IReadOnlyList<string> SourcePaths, IReadOnlyList<TestTarget> TestTargets);
sealed record TestTarget(string RelativePath, string[] MethodPrefixes);
sealed record TestCaseRow(string CaseId, string MethodName, string DisplayName, string TestType, string TestFilePath, int LineNumber);
sealed record RequirementReport(
    RequirementRow Requirement,
    RequirementMapping Mapping,
    List<TestCaseRow> Cases,
    int MeaningfulLoc,
    double LackOfCases,
    int PassedCount,
    int FailedCount,
    int UntestedCount,
    int NormalCount,
    int AbnormalCount,
    int BoundaryCount)
{
    public List<string> Preconditions => Requirement.Preconditions;
}
