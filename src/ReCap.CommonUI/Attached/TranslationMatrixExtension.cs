using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ReCap.CommonUI
{
    public class TranslationMatrixExtension
        : MarkupExtension
    {
        readonly Vector _translation;
        public TranslationMatrixExtension(double x, double y)
            : this(new Vector(x, y))
        {}
        public TranslationMatrixExtension(string vectorStr)
            : this(Vector.Parse(vectorStr))
        {}
        public TranslationMatrixExtension(Vector translation)
        {
            _translation = translation;
        }


        public object ProvideValue()
            => new MatrixTransform(Matrix.CreateTranslation(_translation));
        public override object ProvideValue(IServiceProvider serviceProvider)
            => ProvideValue();
    }
}