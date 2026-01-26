using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace ReCap.CommonUI.Controls
{
    //https://gist.github.com/gmanny/7450651
    //https://stackoverflow.com/questions/806777/wpf-how-can-i-center-all-items-in-a-wrappanel/7747002#7747002

    /// <inheritdoc cref="WrapPanel"/>
    public class AlignableWrapPanel
        : Panel
    {
        /// <summary>
        /// Defines the <see cref="HorizontalContentAlignment"/> property.
        /// </summary>
        public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
            AvaloniaProperty.Register<AlignableWrapPanel, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);

        /// <inheritdoc cref="ContentControl.HorizontalContentAlignment"/>
        public HorizontalAlignment HorizontalContentAlignment
        {
            get => GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }


        static AlignableWrapPanel()
        {
            AffectsArrange<AlignableWrapPanel>(HorizontalContentAlignmentProperty);
        }


        protected override Size MeasureOverride(Size constraint)
        {
            Size curLineSize = new();
            Size panelSize = new();

            var children = Children;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];

                // Flow passes its own constraint to children
                child.Measure(constraint);
                Size sz = child.DesiredSize;

                if (curLineSize.Width + sz.Width > constraint.Width) //need to switch to another line
                {
                    panelSize = new Size(Math.Max(curLineSize.Width, panelSize.Width), panelSize.Height + curLineSize.Height);
                    curLineSize = sz;

                    if (sz.Width > constraint.Width) // if the element is wider then the constraint - give it a separate line                    
                    {
                        panelSize = new Size(Math.Max(sz.Width, panelSize.Width), panelSize.Height + sz.Height);
                        curLineSize = new();
                    }
                }
                else //continue to accumulate a line
                {
                    curLineSize = new Size(curLineSize.Width + sz.Width, Math.Max(sz.Height, curLineSize.Height));
                }
            }

            // the last line size, if any need to be added
            return new Size(Math.Max(curLineSize.Width, panelSize.Width), panelSize.Height + curLineSize.Height);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            int firstInLine = 0;
            Size curLineSize = new();
            double accumulatedHeight = 0;
            var children = Children;

            for (int i = 0; i < children.Count; i++)
            {
                Size sz = children[i].DesiredSize;

                if (curLineSize.Width + sz.Width > arrangeBounds.Width) //need to switch to another line
                {
                    ArrangeLine(accumulatedHeight, curLineSize, arrangeBounds.Width, firstInLine, i);

                    accumulatedHeight += curLineSize.Height;
                    curLineSize = sz;

                    if (sz.Width > arrangeBounds.Width) //the element is wider then the constraint - give it a separate line                    
                    {
                        ArrangeLine(accumulatedHeight, sz, arrangeBounds.Width, i, ++i);
                        accumulatedHeight += sz.Height;
                        curLineSize = new();
                    }
                    firstInLine = i;
                }
                else //continue to accumulate a line
                {
                    curLineSize = new Size(curLineSize.Width + sz.Width, Math.Max(sz.Height, curLineSize.Height));
                }
            }

            if (firstInLine < children.Count)
                ArrangeLine(accumulatedHeight, curLineSize, arrangeBounds.Width, firstInLine, children.Count);

            return arrangeBounds;
        }

        private void ArrangeLine(double y, Size lineSize, double boundsWidth, int start, int end)
        {
            double x = 0;
            if (HorizontalContentAlignment == HorizontalAlignment.Center)
            {
                x = (boundsWidth - lineSize.Width) / 2;
            }
            else if (HorizontalContentAlignment == HorizontalAlignment.Right)
            {
                x = boundsWidth - lineSize.Width;
            }

            var children = Children;
            for (int i = start; i < end; i++)
            {
                var child = children[i];
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineSize.Height));
                x += child.DesiredSize.Width;
            }
        }
    }
}