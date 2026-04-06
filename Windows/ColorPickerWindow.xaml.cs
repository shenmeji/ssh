using System.Windows;
using System.Windows.Media;

namespace SimpleSshClient.Windows
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; set; } = Colors.White;

        public ColorPickerWindow()
        {
            InitializeComponent();
            UpdateSlidersFromColor(SelectedColor);
            UpdateColorPreview();
        }

        private void UpdateSlidersFromColor(Color color)
        {
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            RedValue.Text = color.R.ToString();
            GreenValue.Text = color.G.ToString();
            BlueValue.Text = color.B.ToString();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            byte r = (byte)RedSlider.Value;
            byte g = (byte)GreenSlider.Value;
            byte b = (byte)BlueSlider.Value;
            
            RedValue.Text = r.ToString();
            GreenValue.Text = g.ToString();
            BlueValue.Text = b.ToString();
            
            SelectedColor = Color.FromRgb(r, g, b);
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            ColorPreview.Fill = new SolidColorBrush(SelectedColor);
            ColorHex.Text = SelectedColor.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}