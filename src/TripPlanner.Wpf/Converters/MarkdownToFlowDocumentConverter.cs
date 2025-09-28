using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace TripPlanner.Wpf.Converters
{
    /// <summary>
    /// Minimal Markdown-to-FlowDocument converter supporting:
    /// #, ##, ### headers; unordered lists (- or *); and plain paragraphs.
    /// Keeps scope intentionally small for the Overview description preview.
    /// </summary>
    public sealed class MarkdownToFlowDocumentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var doc = new FlowDocument();
            if (value is not string md || string.IsNullOrWhiteSpace(md))
            {
                doc.Blocks.Add(new Paragraph(new Run("No description yet.")) { Margin = new Thickness(0, 8, 0, 0) });
                return doc;
            }

            Paragraph? currentParagraph = null;
            List? currentList = null;

            void CloseList()
            {
                if (currentList != null)
                {
                    doc.Blocks.Add(currentList);
                    currentList = null;
                }
            }

            foreach (var raw in md.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();

                // Headers
                if (line.StartsWith("# "))
                {
                    CloseList();
                    var p = new Paragraph(new Run(line[2..].Trim()))
                    {
                        FontSize = 22,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 6)
                    };
                    doc.Blocks.Add(p);
                    currentParagraph = null;
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    CloseList();
                    var p = new Paragraph(new Run(line[3..].Trim()))
                    {
                        FontSize = 18,
                        FontWeight = System.Windows.FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    doc.Blocks.Add(p);
                    currentParagraph = null;
                    continue;
                }
                if (line.StartsWith("### "))
                {
                    CloseList();
                    var p = new Paragraph(new Run(line[4..].Trim()))
                    {
                        FontSize = 16,
                        FontWeight = System.Windows.FontWeights.SemiBold,
                        Margin = new Thickness(0, 6, 0, 2)
                    };
                    doc.Blocks.Add(p);
                    currentParagraph = null;
                    continue;
                }

                // Unordered list items
                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    currentParagraph = null;
                    currentList ??= new List();
                    var liText = line[2..].Trim();
                    var li = new ListItem(new Paragraph(new Run(liText)));
                    currentList.ListItems.Add(li);
                    continue;
                }

                // Blank line → paragraph break
                if (string.IsNullOrWhiteSpace(line))
                {
                    CloseList();
                    currentParagraph = null;
                    continue;
                }

                // Plain paragraph (merge consecutive lines)
                CloseList();
                if (currentParagraph == null)
                {
                    currentParagraph = new Paragraph { Margin = new Thickness(0, 2, 0, 8) };
                    doc.Blocks.Add(currentParagraph);
                }
                if (currentParagraph.Inlines.Count > 0)
                {
                    currentParagraph.Inlines.Add(new LineBreak());
                }
                currentParagraph.Inlines.Add(new Run(line));
            }

            CloseList();
            return doc;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    /// <summary>
    /// true → Collapsed, false → Visible (for toggling read-only/edit sections)
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value is bool v && v;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
