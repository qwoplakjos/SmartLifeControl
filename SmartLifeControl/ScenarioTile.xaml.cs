using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartLifeControl
{
    /// <summary>
    /// Логика взаимодействия для ScenarioTile.xaml
    /// </summary>
    public partial class ScenarioTile : UserControl
    {
        public string id = "";
        public string name = "";

        public event EventHandler TileClicked;

        private ScaleTransform TileTransform;


        public ScenarioTile()
        {
            InitializeComponent();
        }

        public void SetTileColor(Color color)
        {
  
            if (MainButton.IsLoaded)
            {
                ApplyTileColor(color);
            }
            else
            {
                MainButton.Loaded += (s, e) => ApplyTileColor(color);
            }
        }

        private void ApplyTileColor(Color c)
        {
            var tileBorder = (Border)MainButton.Template.FindName("TileBorder", MainButton);

            TileTransform = (ScaleTransform)MainButton.Template.FindName("TileScaleTransform", MainButton);

            if (tileBorder != null)
            {
                tileBorder.BorderBrush = new SolidColorBrush(c);
                MainButton.Background = new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B));
            }
        }

        public void SetTileText(string text)
        {
            MainButton.Content = text;
            name = text;
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            TileClicked?.Invoke(this, EventArgs.Empty);
            AnimateScale(TileTransform, 1, 0.90);
        }

        private void MainButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimateScale(TileTransform, 0.90, 1);
        }

        private void MainButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if ( TileTransform.ScaleX < 1) return;
            AnimateScale(TileTransform, 1, 0.95);
        }

        private void MainButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (TileTransform.ScaleX >= 1) return;
            AnimateScale(TileTransform, 0.95, 1);
        }

  

        private void AnimateScale(ScaleTransform transform, double from, double to)
        {
           
            
            
            var anim = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(100)
            };

          

            transform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);


        }


    }
}
