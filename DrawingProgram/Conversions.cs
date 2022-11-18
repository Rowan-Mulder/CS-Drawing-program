using System.Windows.Media;

namespace DrawingProgram
{
    class Conversions
    {
        public string IntToHexidecimal(int value)
        {
            int hex2 = value % 16;
            int hex1 = (value - hex2) / 16;

            return $"{NumToString(hex1)}{NumToString(hex2)}";
        }

        public string NumToString(int num)
        {
            string character;

            switch (num)
            {
                case 10:
                    character = "A";
                    break;
                case 11:
                    character = "B";
                    break;
                case 12:
                    character = "C";
                    break;
                case 13:
                    character = "D";
                    break;
                case 14:
                    character = "E";
                    break;
                case 15:
                    character = "F";
                    break;
                default:
                    character = num.ToString();
                    break;
            }

            return character;
        }

        public Color DrawingcolorToMediacolor(System.Drawing.Color color)
        {
            return Color.FromRgb(color.R, color.G, color.B);
        }

        public System.Drawing.Color MediacolorToDrawingcolor(Color color)
        {
            return System.Drawing.Color.FromArgb(color.R, color.G, color.B);
        }

        public System.Drawing.Color BrushToColor(Brush brush)
        {
            SolidColorBrush extractColorFromBrush = (SolidColorBrush)brush;
            return MediacolorToDrawingcolor(extractColorFromBrush.Color);
        }

        public Brush ColorToBrush(System.Drawing.Color color)
        {
            BrushConverter exportColorToBrush = new BrushConverter();
            return (Brush)exportColorToBrush.ConvertFromString($"#{IntToHexidecimal(color.R)}{IntToHexidecimal(color.G)}{IntToHexidecimal(color.B)}");
        }
    }
}
