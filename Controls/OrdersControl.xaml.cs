using ComputerServiceManager;
using ComputerServiceManager.Services;
using ComputerServiceManager.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ComputerServiceManager.Controls
{
    public partial class OrdersControl : UserControl
    {
        private ObservableCollection<Заказ> _fullOrderList;
        private ICollectionView _ordersView;
        private readonly OrderService _orderService;

        public OrdersControl()
        {
            InitializeComponent();
            _orderService = new OrderService();
            LoadData();
        }

        public void LoadData()
        {
            using (var context = ComputerServiceManagerEntities.GetContext())
            {
                // Используем AsNoTracking() чтобы объекты не отслеживались контекстом
                var orders = context.Заказ
                    .AsNoTracking()
                    .Include(z => z.Клиент)
                    .Include(z => z.Пользователь)
                    .Include(z => z.Статус)
                    .Include(z => z.ТипУстройства)
                    .ToList();

                _fullOrderList = new ObservableCollection<Заказ>(orders);
                _ordersView = CollectionViewSource.GetDefaultView(_fullOrderList);
                _ordersView.Filter = FilterOrders;
                OrdersDataGrid.ItemsSource = _ordersView;

                // --- Загрузка статусов ТОЛЬКО для заказов (idТипСтатуса = 1) ---
                var statuses = context.Статус.ToList();
                // Добавляем "Все"
                statuses.Insert(0, new Статус { idСтатус = -1, Наименование = "Все" });
                cmbFilterStatus.ItemsSource = statuses;
                cmbFilterStatus.SelectedIndex = 0;

                // --- Загрузка типов устройств (все, без фильтрации по типу) ---
                var deviceTypes = context.ТипУстройства.ToList();
                deviceTypes.Insert(0, new ТипУстройства { idТипУстройства = -1, Наименование = "Все" });
                cmbFilterTypeDevice.ItemsSource = deviceTypes;
                cmbFilterTypeDevice.SelectedIndex = 0;

                _fullOrderList = new ObservableCollection<Заказ>(orders);
            }
        }

        private void buttonSearch_Click(object sender, RoutedEventArgs e)
        {
            _ordersView.Refresh();
        }

        public bool FilterOrders(object item)
        {
            if (!(item is Заказ order))
                return false;

            // Фильтр по тексту (поиск по технику, клиенту, устройству)
            string searchText = searchTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                bool matchesDevice = order.ИмяУстройства?.ToLower().Contains(searchText) == true;
                bool matchesClient = order.Клиент?.ФИО?.ToLower().Contains(searchText) == true;
                bool matchesClientPhoneNumber = order.Клиент?.НомерТелефона?.ToLower().Contains(searchText) == true;
                bool matchesTechnician = order.Пользователь?.ФИО?.ToLower().Contains(searchText) == true;
                
                if (!matchesDevice && !matchesClient && !matchesTechnician && !matchesClientPhoneNumber)
                {
                    return false;
                }
            }

            // Фильтр по статусу
            int selectedStatusId = (int)(cmbFilterStatus.SelectedValue ?? -1);
            if (selectedStatusId != -1 && order.idСтатус != selectedStatusId)
            {
                return false;
            }

            // Фильтр по типу устройства
            int selectedTypeId = (int)(cmbFilterTypeDevice.SelectedValue ?? -1);
            if (selectedTypeId != -1 && order.idТипУстройства != selectedTypeId)
            {
                return false;
            }

            // Фильтр по дате
            if (dateFromDatePicker.SelectedDate.HasValue)
            {
                DateTime dateFrom = dateFromDatePicker.SelectedDate.Value.Date;
                if (!order.ДатаЗаказа.HasValue || order.ДатаЗаказа.Value.Date != dateFrom)
                {
                    return false;
                }
            }

            return true;
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ordersView.Refresh();
        }

        private void buttonBack_Click(object sender, RoutedEventArgs e) { }

        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new OrderEditWindow(null);
            editWindow.ShowDialog();
            LoadData();
        }

        private void buttonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersDataGrid.SelectedItem is Заказ selectedOrder)
            {
                var editWindow = new OrderEditWindow(selectedOrder);
                editWindow.ShowDialog();
                LoadData();
            }
            else
            {
                MessageBox.Show("Выберите заказ для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersDataGrid.SelectedItem is Заказ orderFromView)
            {
                int orderId = orderFromView.idЗаказ;

                if (MessageBox.Show("Вы уверены, что хотите удалить этот заказ?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _orderService.DeleteOrder(orderId);
                        MessageBox.Show("Заказ успешно удалён.");
                        LoadData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите заказ для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void cmbFilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ordersView.Refresh();
        }

        private void dateFromDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { }

        private void btnResetDatePicker_Click(object sender, RoutedEventArgs e)
        {
            dateFromDatePicker.SelectedDate = null;
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}