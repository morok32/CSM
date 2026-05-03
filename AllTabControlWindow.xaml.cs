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
using ComputerServiceManager.Controls;

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

        /// <summary>
        /// Применяет ограничения доступа на основе роли текущего пользователя
        /// </summary>
        public void ApplyRoleBasedAccess()
        {
            // Если пользователь не авторизован - закрываем окно
            if (!AuthService.IsAuthenticated)
            {
                MessageBox.Show("Ошибка авторизации", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            // Получаем роль текущего пользователя
            string roleName = AuthService.CurrentUserRole;
            bool isTechnician = AuthService.IsTechnician;
            bool isAdmin = AuthService.IsAdmin;

            // Ограничения для Техника
            if (isTechnician)
            {
                // Делает вкладку "Услуги" неактивной
                var servicesTab = MainTabControl.Items.OfType<TabItem>()
                    .FirstOrDefault(t => t.Header?.ToString() == "Услуги");
                if (servicesTab != null)
                    servicesTab.IsEnabled = false;

                // Делает вкладку "Пользователи" неактивной
                var usersTab = MainTabControl.Items.OfType<TabItem>()
                    .FirstOrDefault(t => t.Header?.ToString() == "Пользователи");
                if (usersTab != null)
                    usersTab.IsEnabled = false;

                // Для вкладки Материалы - блокируем вкладку "Редактировать" и кнопки
                if (MaterialsControl != null)
                {
                    MaterialsControl.SetTechnicianMode(true);
                }
            }

            // Фильтрация заказов в контроле OrdersControl
            if (OrdersControl != null)
            {
                OrdersControl.ApplyUserFilter(AuthService.CurrentUserId);
            }

            // Для администратора никаких ограничений не применяем
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
