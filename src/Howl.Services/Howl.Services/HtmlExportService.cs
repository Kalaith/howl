using System;
using System.IO;
using System.Linq;
using System.Text;
using Howl.Core.Models;
using Howl.Services.Models;

namespace Howl.Services;

public class HtmlExportService
{
    public string ExportToHtml(GeminiResponse response, RecordingSession session, string outputPath)
    {
        var html = GenerateHtml(response);
        var outputDir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(outputDir);

        // Write HTML file
        File.WriteAllText(outputPath, html);

        // Copy screenshots to output directory
        if (response.Instructions != null)
        {
            foreach (var step in response.Instructions)
            {
                if (!string.IsNullOrEmpty(step.ScreenshotReference))
                {
                    var sourceFile = Path.Combine(session.OutputDirectory!, "frames", step.ScreenshotReference);

                    if (File.Exists(sourceFile))
                    {
                        var destFile = Path.Combine(outputDir, step.ScreenshotReference);
                        File.Copy(sourceFile, destFile, true);
                    }
                }
            }
        }

        return outputPath;
    }

    public string ExportToZip(GeminiResponse response, RecordingSession session, string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Howl", $"export_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Generate HTML in temp directory
            var htmlPath = Path.Combine(tempDir, "index.html");
            ExportToHtml(response, session, htmlPath);

            // Create ZIP
            System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath,
                System.IO.Compression.CompressionLevel.Optimal, false);

            return zipPath;
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private string GenerateHtml(GeminiResponse response)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>{EscapeHtml(response.Title)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            max-width: 900px;
            margin: 0 auto;
            padding: 40px 20px;
            line-height: 1.6;
            color: #333;
        }
        h1 {
            color: #2c3e50;
            border-bottom: 3px solid #3498db;
            padding-bottom: 10px;
        }
        .meta {
            color: #7f8c8d;
            font-size: 0.9em;
            margin-bottom: 30px;
        }
        .summary {
            background: #ecf0f1;
            padding: 15px 20px;
            border-left: 4px solid #3498db;
            margin: 20px 0;
        }
        .prerequisites {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px 20px;
            margin: 20px 0;
        }
        .prerequisites h3 {
            margin-top: 0;
            color: #856404;
        }
        .prerequisites ul {
            margin-bottom: 0;
        }
        ol {
            counter-reset: step-counter;
            list-style: none;
            padding-left: 0;
        }
        ol li {
            counter-increment: step-counter;
            margin-bottom: 40px;
            position: relative;
        }
        ol li::before {
            content: counter(step-counter);
            background: #3498db;
            color: white;
            font-weight: bold;
            font-size: 1.2em;
            width: 40px;
            height: 40px;
            border-radius: 50%;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            margin-right: 15px;
            flex-shrink: 0;
        }
        .step-content {
            display: inline-block;
            vertical-align: top;
            width: calc(100% - 60px);
        }
        .step-content img {
            max-width: 100%;
            height: auto;
            border: 1px solid #ddd;
            border-radius: 4px;
            margin-top: 10px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        .step-content p {
            margin: 0 0 10px 0;
            font-size: 1.1em;
        }
        footer {
            margin-top: 60px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #7f8c8d;
            font-size: 0.9em;
        }
    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Title
        sb.AppendLine($"    <h1>{EscapeHtml(response.Title)}</h1>");
        sb.AppendLine($"    <p class=\"meta\">Recorded with <strong>Howl</strong></p>");

        // Summary
        if (!string.IsNullOrWhiteSpace(response.Summary))
        {
            sb.AppendLine("    <div class=\"summary\">");
            sb.AppendLine($"        <p>{EscapeHtml(response.Summary)}</p>");
            sb.AppendLine("    </div>");
        }

        // Prerequisites
        if (response.Prerequisites != null && response.Prerequisites.Length > 0)
        {
            sb.AppendLine("    <div class=\"prerequisites\">");
            sb.AppendLine("        <h3>Prerequisites</h3>");
            sb.AppendLine("        <ul>");
            foreach (var prereq in response.Prerequisites)
            {
                sb.AppendLine($"            <li>{EscapeHtml(prereq)}</li>");
            }
            sb.AppendLine("        </ul>");
            sb.AppendLine("    </div>");
        }

        // Steps
        if (response.Instructions != null && response.Instructions.Length > 0)
        {
            sb.AppendLine("    <ol>");
            foreach (var step in response.Instructions)
            {
                sb.AppendLine("        <li>");
                sb.AppendLine("            <div class=\"step-content\">");
                sb.AppendLine($"                <p>{EscapeHtml(step.Instruction)}</p>");
                if (!string.IsNullOrWhiteSpace(step.ScreenshotReference))
                {
                    sb.AppendLine($"                <img src=\"{EscapeHtml(step.ScreenshotReference)}\" alt=\"Step {step.StepNumber}\">");
                }
                sb.AppendLine("            </div>");
                sb.AppendLine("        </li>");
            }
            sb.AppendLine("    </ol>");
        }

        // Footer
        sb.AppendLine("    <footer>");
        sb.AppendLine($"        <p>Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("    </footer>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
