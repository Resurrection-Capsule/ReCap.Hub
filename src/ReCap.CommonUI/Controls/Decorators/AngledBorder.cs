using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReCap.CommonUI.Controls.Decorators
{
    /// <summary>
    /// A control which decorates a child with a border and background. Similar to <see cref="Border"/>, but with 45-degree angle corners instead of rounded corners.
    /// </summary>
    public class AngledBorder
        : AngledBorderBase
    {
        /// Defines the <see cref="CornerRadius"/> property.
        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            Border.CornerRadiusProperty.AddOwner<AngledBorder>();

        /// <summary>
        /// Gets or sets the radius of the border angled corners.
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        static AngledBorder()
        {
            AffectsGeometry<AngledBorder>(CornerRadiusProperty);
            AffectsRender<AngledBorder>(CornerRadiusProperty);
        }

        protected override void RefreshGeometry(out Geometry fillGeometry, out Geometry strokeGeometry, out RoundedRect glowRect)
        {
            //Console.WriteLine($"Updating geometries...");
            double width = Math.Round(Bounds.Width);
            double height = Math.Round(Bounds.Height);

            double minDimen = Math.Min(width, height);
            
            CornerRadius radius = CornerRadius;
            double tl = Math.Min(radius.TopLeft, minDimen);
            double tr = Math.Min(radius.TopRight, minDimen);
            double br = Math.Min(radius.BottomRight, minDimen);
            double bl = Math.Min(radius.BottomLeft, minDimen);
            
            /*tl = Math.Round(tl, 0);
            tr = Math.Round(tr, 0);
            br = Math.Round(br, 0);
            bl = Math.Round(bl, 0);*/
            
            double strokeThickness = StrokeThickness;
            double borderBothSides = strokeThickness * 2;
            double fillWidth = width - borderBothSides;
            double fillHeight = height - borderBothSides;
            var rect = new Rect(strokeThickness, strokeThickness, fillWidth, fillHeight);
            glowRect = new RoundedRect(rect, tl, tr, br, bl);
            
            var fillGeom = new StreamGeometry();
            using (var ctx = fillGeom.Open())
            {
                ctx.CreateGeometry(strokeThickness, strokeThickness, fillWidth, fillHeight, tl, tr, br, bl, true);
            }
            fillGeometry = fillGeom;

            /*double realAverageBorderThickness = _averageBorderThickness;
            _averageBorderThickness = 4;
            borderBothSides = _averageBorderThickness * 2;
            fillWidth = width - borderBothSides;
            fillHeight = height - borderBothSides;*/

            strokeGeometry = null;
            if (strokeThickness > 0)
            {
                double diagonalDiff = strokeThickness / 2;

                tl = (tl > 0) ? Math.Round(tl + diagonalDiff, 0) : 0;
                tr = (tr > 0) ? Math.Round(tr + diagonalDiff, 0) : 0;
                br = (br > 0) ? Math.Round(br + diagonalDiff, 0) : 0;
                bl = (bl > 0) ? Math.Round(bl + diagonalDiff, 0) : 0;
                var strokeOuterGeometry = new StreamGeometry();
                using (var ctx = strokeOuterGeometry.Open())
                {
                    ctx.CreateGeometry(0, 0, width, height, tl, tr, br, bl, true);
                    //////ctx.BeginFigure(new Point(0 + 1, 0 + tl), true);
                    //////ctx.TraverseGeometry(0, 0, width, height, tl, tr, br, bl, true);
                    //ctx.TraverseGeometry(_averageBorderThickness, _averageBorderThickness, fillWidth, fillHeight, tl - _averageBorderThickness, tr - _averageBorderThickness, br - _averageBorderThickness, bl - _averageBorderThickness, false);
                    //////ctx.EndFigure(true);
                }

                //_averageBorderThickness = realAverageBorderThickness;
                //strokeGeometry = new CombinedGeometry(GeometryCombineMode.Xor, strokeOuterGeometry, fillGeometry/*, new TranslateTransform(0, 0)*/);
                strokeGeometry = strokeOuterGeometry;
            }
        }
    }
}