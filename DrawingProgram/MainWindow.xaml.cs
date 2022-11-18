using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/**
 * TODO: Fixes/Optimization
 *          - Anti-aliasing
 *              - XAML attribute RenderOptions.BitmapScalingMode="NearestNeighbor" allows an InkCanvas to render aliased, which may be preferred. At runtime, changing RenderOptions.SetBitmapScalingMode(inkCanvas, BitmapScalingMode.NearestNeighbor); doesn't seem to work, as the parameter inkCanvas is probably incorrect.
 *          - Selections
 *              - Scaling only affects the position of StylusPoints from Strokes, not the size
 *              - Only lasso-select is currently available. Add rectangle-select too and change lasso-select its icon
 *          - Fill-tool
 *              - Occasionally fills strokes that aren't close to the cursor
 */

namespace DrawingProgram
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly Conversions Conversions = new Conversions();
        readonly Calculations Calculations = new Calculations();
        Collection<StrokeCollection> InkCanvasHistory = new Collection<StrokeCollection>();
        int historyDepth = 0;
        int historyNodesDeleted = 0;
        Collection<System.Drawing.Color> CustomBrushColors = new Collection<System.Drawing.Color>();
        Collection<System.Drawing.Color> CustomBGColors = new Collection<System.Drawing.Color>();
        bool FillToolEnabled = false;
        bool ResizingHookEnabled = false;
        bool SliderResizingInit = false;
        bool Cursive = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CanvasSizeChanged();
            SliderResizingInit = true;
            inkCanvas.AddHandler(InkCanvas.MouseDownEvent, new MouseButtonEventHandler(InkCanvas_MouseDown), true);
            inkCanvas.AddHandler(InkCanvas.MouseUpEvent, new MouseButtonEventHandler(InkCanvas_MouseUp), true);
            InkCanvasHistory.Add(inkCanvas.Strokes.Clone());
            inkCanvas.DefaultDrawingAttributes.Width = 2.0;
            UpdateDebugMenu();
        }

        private void CbbFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selected = ((ComboBoxItem)cbbFile.SelectedItem).Content.ToString();

            if (selected != "File")
            {
                switch (selected)
                {
                    case "New":
                        inkCanvas.Strokes.Clear();
                        cbxCursive.IsChecked = false;
                        cbxPencilSmoothing.IsChecked = false;
                        inkCanvas.DefaultDrawingAttributes.FitToCurve = false;
                        sldCanvasWidth.Value = 500;
                        sldCanvasHeight.Value = 300;
                        sldPencilSize.Value = 2;
                        InkCanvasHistory = new Collection<StrokeCollection>();
                        CustomBrushColors = new Collection<System.Drawing.Color>();
                        ChangeCurrentBrushColor(System.Drawing.Color.FromArgb(0, 0, 0));
                        CustomBGColors = new Collection<System.Drawing.Color>();
                        inkCanvas.Background = Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                        btnColorCustomColor.Background = Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                        btnColorCustomBGColor.Background = Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                        btnColorCustomColor.Foreground = Calculations.IsRGBBright(254, 254, 254) ? Conversions.ColorToBrush(System.Drawing.Color.FromArgb(1, 1, 1)) : Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                        btnColorCustomBGColor.Foreground = Calculations.IsRGBBright(254, 254, 254) ? Conversions.ColorToBrush(System.Drawing.Color.FromArgb(1, 1, 1)) : Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                        historyDepth = 0;
                        historyNodesDeleted = 0;
                        InkCanvasHistory.Add(inkCanvas.Strokes.Clone());
                        UpdateDebugMenu();
                        break;
                    case "Open":
                        System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
                        {
                            Filter = "STROKES |*.strokes",
                            DefaultExt = "strokes"
                        };

                        if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            if (openFileDialog.FileName.EndsWith(".strokes"))
                            {
                                try
                                {
                                    var fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                                    StrokeCollection strokes = new StrokeCollection(fs);
                                    inkCanvas.Strokes = strokes;
                                    if (!fs.CanRead)
                                    {
                                        MessageBox.Show("Try running the program as an administrator for elevated Open/Read rights.");
                                    }
                                }
                                catch
                                {
                                    MessageBox.Show($@"Can't read {openFileDialog.FileName}");
                                }
                            }
                            else
                            {
                                MessageBox.Show($@"Can only load (.strokes) files");
                            }

                            UpdateDebugMenu();
                        }

                        break;
                    case "Save":
                        System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog
                        {
                            Filter = "STROKES |*.strokes|JPEG |*.jpg,*.jpeg,*.jpe,*.jfif,*.exif|PNG |*.png",
                            DefaultExt = "strokes",
                            AddExtension = true
                        };

                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            if (saveFileDialog.FileName.EndsWith(".jpeg") || saveFileDialog.FileName.EndsWith(".jpg") || saveFileDialog.FileName.EndsWith(".jpe") || saveFileDialog.FileName.EndsWith(".jfif") || saveFileDialog.FileName.EndsWith(".exif"))
                            {
                                FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create);

                                RenderTargetBitmap rtb = new RenderTargetBitmap((int)inkCanvas.Width, (int)inkCanvas.Height, 96d, 96d, PixelFormats.Default);
                                rtb.Render(inkCanvas);
                                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(rtb));

                                encoder.Save(fs);
                                fs.Close();
                            }
                            else if (saveFileDialog.FileName.EndsWith(".strokes"))
                            {
                                if (inkCanvas.Strokes.Count > 0)
                                {
                                    FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create);
                                    inkCanvas.Strokes.Save(fs);
                                }
                                else
                                {
                                    MessageBox.Show("You haven't drawn any strokes so far.");
                                }
                            }
                            else if (saveFileDialog.FileName.EndsWith(".png"))
                            {
                                MessageBox.Show("Support for PNG has not been included yet.");
                            }
                            else
                            {
                                MessageBox.Show("Support for chosen file extension has not been included yet.");
                            }
                        }

                        break;
                    case "Debug":
                        debugMenu.Visibility = (debugMenu.Visibility == Visibility.Hidden) ? Visibility.Visible : Visibility.Hidden; 
                        break;
                }

                cbbFile.SelectedIndex = 0;
            }
        }

        private void CbbEdit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selected = ((ComboBoxItem)cbbEdit.SelectedItem).Content.ToString();

            if (selected != "Edit")
            {
                switch(selected)
                {
                    case "Undo":
                        if (historyDepth > 0)
                        {
                            inkCanvas.Strokes = InkCanvasHistory[historyDepth - 1].Clone();
                            historyDepth--;
                            UpdateDebugMenu();
                        }

                        break;
                    case "Redo":
                        if (historyDepth + 1 < InkCanvasHistory.Count)
                        {
                            inkCanvas.Strokes = InkCanvasHistory[historyDepth + 1].Clone();
                            historyDepth++;
                            UpdateDebugMenu();
                        }

                        break;
                }

                cbbEdit.SelectedIndex = 0;
            }
        }

        private void BtnToolsSelect_Click(object sender, RoutedEventArgs e)
        {
            FillToolEnabled = false;
            inkCanvas.EditingMode = InkCanvasEditingMode.Select;
        }

        private void BtnToolsBrush_Click(object sender, RoutedEventArgs e)
        {
            FillToolEnabled = false;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void BtnToolsEraserStrokeParts_Click(object sender, RoutedEventArgs e)
        {
            FillToolEnabled = false;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }

        private void BtnToolsEraserStrokes_Click(object sender, RoutedEventArgs e)
        {
            FillToolEnabled = false;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        }

        private void BtnToolsFill_Click(object sender, RoutedEventArgs e)
        {
            FillToolEnabled = true;
            inkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
        }

        private void CbxPencilSmoothing_Click(object sender, RoutedEventArgs e)
        {
            if (cbxPencilSmoothing.IsChecked.Value)
            {
                inkCanvas.DefaultDrawingAttributes.FitToCurve = true;
            }
            else
            {
                inkCanvas.DefaultDrawingAttributes.FitToCurve = false;
            }
        }

        private void CbxCursive_Click(object sender, RoutedEventArgs e)
        {
            if (cbxCursive.IsChecked.Value)
            {
                Cursive = true;
                inkCanvas.DefaultDrawingAttributes.Width = 1;
                pencilPreview.Width = 1;
            }
            else
            {
                Cursive = false;
                inkCanvas.DefaultDrawingAttributes.Width = inkCanvas.DefaultDrawingAttributes.Height;
                pencilPreview.Width = pencilPreview.Height;
            }
        }

        private void SldCanvasWidthValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderResizingInit)
            {
                inkCanvas.Width = e.NewValue;
                CanvasSizeChanged();

                if (!ResizingHookEnabled)
                {
                    Thickness newThickness = resizeHook.Margin;
                    newThickness.Left = inkCanvas.Width;
                    newThickness.Top = inkCanvas.Height;
                    resizeHook.Margin = newThickness;
                }
            }
        }

        private void SldCanvasHeightValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderResizingInit)
            {
                inkCanvas.Height = e.NewValue;
                CanvasSizeChanged();

                if (!ResizingHookEnabled)
                {
                    Thickness newThickness = resizeHook.Margin;
                    newThickness.Left = inkCanvas.Width;
                    newThickness.Top = inkCanvas.Height;
                    resizeHook.Margin = newThickness;
                }
            }
        }

        private void SldCanvasWidth_MouseEnter(object sender, MouseEventArgs e)
        {
            ResizingHookEnabled = false;
        }

        private void SldCanvasHeight_MouseEnter(object sender, MouseEventArgs e)
        {
            ResizingHookEnabled = false;
        }

        private void SldPencilSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (inkCanvas == null)
            {
                return;
            }

            try
            {
                if (!Cursive)
                {
                    inkCanvas.DefaultDrawingAttributes.Width = e.NewValue;
                    pencilPreview.Width = e.NewValue > 2 ? e.NewValue : 2;
                }

                inkCanvas.DefaultDrawingAttributes.Height = e.NewValue;
                pencilPreview.Height = e.NewValue > 2 ? e.NewValue : 2;

                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    inkCanvas.EraserShape = new RectangleStylusShape(sldPencilSize.Value, sldPencilSize.Value);
                    inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                }
            }
            catch
            {

            }
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            ChangeCurrentBrushColor(Conversions.BrushToColor((sender as Button).Background));
        }

        private void BtnColorWhite_Click(object sender, RoutedEventArgs e)
        {
            ChangeCurrentBrushColor(Conversions.BrushToColor((sender as Button).Background));
        }

        private void BtnColorCustomColor_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();

            if (CustomBrushColors.Count == 0)
            {
                colorDialog.Color = System.Drawing.Color.Red;
            }
            else
            {
                colorDialog.Color = CustomBrushColors[CustomBrushColors.Count - 1];
            }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                System.Drawing.Color colorOfChoice = colorDialog.Color;
                ChangeCurrentBrushColor(colorOfChoice);
                btnColorCustomColor.Background = Conversions.ColorToBrush(colorOfChoice);
                btnColorCustomColor.Foreground = Calculations.IsRGBBright(colorOfChoice.R, colorOfChoice.G, colorOfChoice.B) ? Conversions.ColorToBrush(System.Drawing.Color.FromArgb(1, 1, 1)) : Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                CustomBrushColors.Add(colorOfChoice);
            }
        }

        private void BtnColorCustomBGColor_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();

            if (CustomBGColors.Count == 0)
            {
                colorDialog.Color = System.Drawing.Color.Red;
            }
            else
            {
                colorDialog.Color = CustomBGColors[CustomBGColors.Count - 1];
            }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                System.Drawing.Color colorOfChoice = colorDialog.Color;
                ChangeCurrentBGColor(colorOfChoice);
                btnColorCustomBGColor.Background = Conversions.ColorToBrush(colorOfChoice);
                btnColorCustomBGColor.Foreground = Calculations.IsRGBBright(colorOfChoice.R, colorOfChoice.G, colorOfChoice.B) ? Conversions.ColorToBrush(System.Drawing.Color.FromArgb(1, 1, 1)) : Conversions.ColorToBrush(System.Drawing.Color.FromArgb(254, 254, 254));
                CustomBGColors.Add(colorOfChoice);
            }
        }

        private void ChangeCurrentBrushColor(System.Drawing.Color color)
        {
            pencilPreview.Fill = Conversions.ColorToBrush(color);
            inkCanvas.DefaultDrawingAttributes.Color = Conversions.DrawingcolorToMediacolor(color);
        }

        private void ChangeCurrentBGColor(System.Drawing.Color color)
        {
            inkCanvas.Background = Conversions.ColorToBrush(color);
        }

        private void ResizeHook_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ResizingHookEnabled = true;
        }

        private void ResizeHook_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ResizingHookEnabled = false;
        }

        private void ResizeField_MouseMove(object sender, MouseEventArgs e)
        {
            if (ResizingHookEnabled)
            {
                Thickness newThickness = resizeHook.Margin;
                newThickness.Left = e.GetPosition(this.resizeField).X - 14 > 0 ? e.GetPosition(this.resizeField).X - 14 : 1;
                newThickness.Top = e.GetPosition(this.resizeField).Y - 14 > 0 ? e.GetPosition(this.resizeField).Y - 14 : 1;
                resizeHook.Margin = newThickness;

                inkCanvas.Width = newThickness.Left;
                inkCanvas.Height = newThickness.Top;
                CanvasSizeChanged();

                if (Math.Floor((double)lblCanvasHeight.Content) <= 1 || Math.Floor((double)lblCanvasWidth.Content) <= 1)
                {
                    ResizingHookEnabled = false;
                }
            }
        }

        private void CanvasSizeChanged()
        {
            sldCanvasWidth.Value = inkCanvas.Width;
            sldCanvasHeight.Value = inkCanvas.Height;

            lblCanvasWidth.Content = inkCanvas.Width;
            lblCanvasHeight.Content = inkCanvas.Height;
        }

        private void ChangeStrokeColor(int strokeIndex, Color color)
        {
            Stroke stroke = inkCanvas.Strokes[strokeIndex];
            stroke.DrawingAttributes.Color = color;
            inkCanvas.Strokes.Remove(inkCanvas.Strokes[strokeIndex]);
            inkCanvas.Strokes.Add(stroke);
        }

        private void InkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (FillToolEnabled)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;

                Collection<int> strokesToAlter = new Collection<int>();
                double cursorX = e.GetPosition(this.inkCanvas).X;
                double cursorY = e.GetPosition(this.inkCanvas).Y;

                foreach (Stroke stroke in inkCanvas.Strokes)
                {
                    foreach (StylusPoint stylusPoint in stroke.StylusPoints)
                    {
                        if (stylusPoint.X < (cursorX + (sldPencilSize.Value / 2)) && stylusPoint.X > (cursorX - (sldPencilSize.Value / 2)) && stylusPoint.Y < (cursorY + (sldPencilSize.Value / 2)) && stylusPoint.Y > (cursorY - (sldPencilSize.Value / 2)))
                        {
                            if (!strokesToAlter.Contains(inkCanvas.Strokes.IndexOf(stroke)))
                            {
                                strokesToAlter.Add(inkCanvas.Strokes.IndexOf(stroke));
                            }
                        }
                    }
                }

                foreach (int strokeToAlter in strokesToAlter)
                {
                    ChangeStrokeColor(strokeToAlter, Conversions.DrawingcolorToMediacolor(Conversions.BrushToColor(pencilPreview.Fill)));
                }
            }
        }

        private void InkCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (FillToolEnabled)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
            }

            if ((historyDepth + 1) < InkCanvasHistory.Count)
            {
                int removeHistoryIterations = (InkCanvasHistory.Count - (historyDepth + 1));

                for (int i = 0; i < removeHistoryIterations; i++)
                {
                    InkCanvasHistory.RemoveAt((InkCanvasHistory.Count - 1));
                    historyNodesDeleted++;
                }

                InkCanvasHistory.Add(inkCanvas.Strokes.Clone());
            }

            InkCanvasHistory.Add(inkCanvas.Strokes.Clone());
            historyDepth++;

            UpdateDebugMenu();
        }

        private void UpdateDebugMenu()
        {
            debugHistorySize.Content = InkCanvasHistory.Count;
            debugHistoryNodesDeleted.Content = historyNodesDeleted;
            debugCurrentHistoryNode.Content = (historyDepth + 1);
            debugStrokesAmount.Content = inkCanvas.Strokes.Count;

            int stylusPointsCount = 0;
            
            foreach (Stroke stroke in inkCanvas.Strokes)
            {
                stylusPointsCount += stroke.StylusPoints.Count;
            }

            debugStylusPointsAmount.Content = stylusPointsCount;
        }
    }
}
