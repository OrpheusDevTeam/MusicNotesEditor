using Manufaktura.Controls.Model;
using Manufaktura.Controls.Rendering;
using Manufaktura.Controls.WPF;
using Manufaktura.Controls.WPF.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicNotesEditor.Helpers
{
    public class SelectionHelper
    {

        public static WpfCanvasScoreRenderer GetRenderer(NoteViewer noteViewer)
        {
            var rendererProp = typeof(NoteViewer)
                .GetProperty("Renderer", BindingFlags.NonPublic | BindingFlags.Instance);

            WpfCanvasScoreRenderer scoreRenderer = rendererProp?.GetValue(noteViewer) as WpfCanvasScoreRenderer;

            if (scoreRenderer == null)
            {
                throw new InvalidOperationException("Failed to retrieve 'Renderer' field from NoteViewer or it is not of type WpfCanvasScoreRenderer.");
            }
            return scoreRenderer;
        }

        public static Dictionary<FrameworkElement, MusicalSymbol> GetOwnershipDictionary(NoteViewer noteViewer)
        {
            return GetRenderer(noteViewer).OwnershipDictionary;
        }

        public static void ColorElement(NoteViewer noteViewer, MusicalSymbol element, Color? color = null)
        {
            var elements = new List<MusicalSymbol>() { element };
            ColorElements(noteViewer, elements, color);
        }


        public static void ColorElements(NoteViewer noteViewer, List<MusicalSymbol> elements, Color? color = null)
        {
            var ownershipDictionary = GetOwnershipDictionary(noteViewer);

            if (color == null)
            {
                color = Color.FromRgb(0, 0, 0);
            }

            foreach (var element in elements)
            {
                IEnumerable<KeyValuePair<FrameworkElement, MusicalSymbol>> enumerable = ownershipDictionary.Where(
                    (KeyValuePair<FrameworkElement, MusicalSymbol> o) => o.Value == element);
                foreach (KeyValuePair<FrameworkElement, MusicalSymbol> item in enumerable)
                {
                    if (item.Key is TextBlock textBlock)
                    {
                        textBlock.Foreground = new SolidColorBrush(color.Value);
                    }

                    if (item.Key is Shape shape)
                    {
                        shape.Stroke = new SolidColorBrush(color.Value);
                    }
                }
            }
        }


    }
}
