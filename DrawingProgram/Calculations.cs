using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DrawingProgram
{
    class Calculations
    {
        public bool IsRGBBright(int r, int g, int b)
        {
            double summedUp = (r + g + b);
            return summedUp > 400;
        }
    }
}
