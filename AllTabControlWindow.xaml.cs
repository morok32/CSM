using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ComputerServiceManager
{
    /// <summary>
    /// Логика взаимодействия для AllTabControlWindow.xaml
    /// </summary>
    public partial class AllTabControlWindow : Window
    {
        public AllTabControlWindow()
        {
            InitializeComponent();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, была ли выбрана вкладка "Материалы"
            if (e.Source is TabControl tabControl && e.AddedItems.Count > 0)
            {
                var selectedTab = e.AddedItems[0] as TabItem;
                if (selectedTab?.Header?.ToString() == "Материалы")
                {
                    // Обновляем данные на вкладке Материалы
                    MaterialsControl?.LoadData();
                }
            }
        }
    }
}
