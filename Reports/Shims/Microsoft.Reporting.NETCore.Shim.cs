namespace Microsoft.Reporting.NETCore
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    // Minimal shim types to allow compilation when the real package isn't available in the feed.
    // These implementations are NOT full-featured. Replace with the official `Microsoft.Reporting.NETCore` package
    // and remove this file when you can restore from NuGet.

    public class LocalReport : IDisposable
    {
        public string ReportPath { get; set; } = string.Empty;

        public List<ReportDataSource> DataSources { get; } = new List<ReportDataSource>();

        public byte[] Render(string format)
        {
            // Very small placeholder: return a PDF header and a single-page dummy PDF file.
            if (!string.Equals(format, "PDF", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only PDF rendering is supported by the shim.");

            // Minimal valid PDF content with one blank page.
            var pdf = new byte[] {
                0x25,0x50,0x44,0x46,0x2D,0x31,0x2E,0x34,0x0A, // %PDF-1.4\n
                // rest is not a full PDF; this is just a placeholder and will not display complex content.
                0x25,0xE2,0xE3,0xCF,0xD3,0x0A
            };

            return pdf;
        }

        public void Dispose()
        {
        }
    }

    public class ReportDataSource
    {
        public string Name { get; }
        public object Value { get; }

        public ReportDataSource(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}
