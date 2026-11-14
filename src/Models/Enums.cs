// Enumerations: tools, object kinds, pointer modes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WhiteboardApp
{
    enum Tool { Select, Hand, Pen, Highlighter, Eraser, Line, Arrow, Rect, Ellipse, Triangle, Star, Text, Note }
    enum Kind { Stroke, Line, Arrow, Rect, Ellipse, Triangle, Star, Text, Note, Image, Ruler, Protractor, Axes }
    enum Mode { None, Pan, Move, Marquee, Erase, Draw, Resize, Endpoint, Rotate }
}
