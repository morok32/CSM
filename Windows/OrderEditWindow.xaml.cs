using ComputerServiceManager.Models;
using ComputerServiceManager.Services;
using ComputerServiceManager.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerServiceManager.Windows
{
    public partial class OrderEditWindow : Window
    {
        private Заказ _currentOrder;
        private ObservableCollection<OrderMaterialItem> _materials;
        private ObservableCollection<OrderServiceItem> _services;
        private bool _isNewOrder;
        private ComputerServiceManagerEntities _dictContext;
        private readonly OrderService _orderService;

        public OrderEditWindow(Заказ order)
        {
            InitializeComponent();
            _isNewOrder = order == null;
            _dictContext = new ComputerServiceManagerEntities();
            _orderService = new OrderService();

            if (_isNewOrder)
            {
                _currentOrder = new Заказ
                {
                    ДатаЗаказа = DateTime.Now,
                    idСтатус = 1, // Новый статус
                    Клиент = new Клиент()
                };
                _materials = new ObservableCollection<OrderMaterialItem>();
                _services = new ObservableCollection<OrderServiceItem>();
            }
            else
            {
                _currentOrder = order;
                var items = _orderService.GetOrderItems(order.idЗаказ);
                _materials = new ObservableCollection<OrderMaterialItem>(items.Materials);
                _services = new ObservableCollection<OrderServiceItem>(items.Services);
            }

            DataContext = _currentOrder;
            dgMaterials.ItemsSource = _materials;
            dgServices.ItemsSource = _services;

            LoadDictionaries();
            UpdateTotalAmount();
        }

        private void LoadDictionaries()
        {
            cmbxStatusInCard.ItemsSource = _dictContext.Статус.Where(t => t.ТипСтатуса.idТипСтатуса == 1).ToList();
            cmbxStatusInCard.DisplayMemberPath = "Наименование";
            cmbxStatusInCard.SelectedValuePath = "idСтатус";
            cmbxStatusInCard.SelectedValue = _currentOrder.idСтатус;

            // Подписываемся на событие изменения статуса
            cmbxStatusInCard.SelectionChanged += cmbxStatusInCard_SelectionChanged;

            // Блокировка ComboBox статуса для отмененных заказов (статус 7)
            if (_currentOrder.idСтатус == 7)
            {
                cmbxStatusInCard.IsEnabled = false;
            }

            cmbxTypeDevice.ItemsSource = _dictContext.ТипУстройства.ToList();
            cmbxTypeDevice.DisplayMemberPath = "Наименование";
            cmbxTypeDevice.SelectedValuePath = "idТипУстройства";
            cmbxTypeDevice.SelectedValue = _currentOrder.idТипУстройства;

            var techniciansList = _dictContext.Пользователь.Where(p => p.Активность == true).ToList();
            cmbxTechnician.ItemsSource = techniciansList;
            cmbxTechnician.SelectedValuePath = "idПользователь";

            // Если это новый заказ и пользователь авторизован как техник,
            // автоматически выбираем его и блокируем ComboBox
            if (_isNewOrder && AuthService.IsTechnician && AuthService.CurrentUserId.HasValue)
            {
                cmbxTechnician.SelectedValue = AuthService.CurrentUserId.Value;
                cmbxTechnician.IsEnabled = false;
            }

            var materialsList = _dictContext.Материал.Where(m => m.idТипМатериала == 1).ToList();
            cmbMaterialSelect.ItemsSource = materialsList;
            cmbMaterialSelect.DisplayMemberPath = "Наименование";
            cmbMaterialSelect.SelectedValuePath = "idМатериала";

            var servicesList = _dictContext.Услуги.ToList();
            cmbServiceSelect.ItemsSource = servicesList;
            cmbServiceSelect.DisplayMemberPath = "Наименование";
            cmbServiceSelect.SelectedValuePath = "idУслуги";
        }

        private void btnAddMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (cmbMaterialSelect.SelectedItem is Материал selectedMat)
            {
                var newItem = new OrderMaterialItem
                {
                    idМатериала = selectedMat.idМатериал,
                    Наименование = selectedMat.Наименование,
                    Количество = 1,
                    // Безопасное приведение nullable decimal
                    ЦенаЗаЕдиницу = selectedMat.РозничнаяЦена ?? selectedMat.БазоваяСтоимость ?? 0,
                    // Новый материал автоматически резервируется (idСтатус = 8 "Резерв")
                    idСтатус = 8, // По умолчанию материал ЗАРЕЗЕРВИРОВАН при добавлении
                    IsNew = true
                };
                _materials.Add(newItem);
                UpdateTotalAmount();
                cmbMaterialSelect.SelectedIndex = -1;
            }
        }

        private void btnRemoveMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem is OrderMaterialItem item)
            {
                // Удаляем позицию из списка (резерв автоматически снимается при сохранении)
                if (item.idПозиции == 0)
                {
                    _materials.Remove(item);
                    UpdateTotalAmount();
                    dgMaterials.Items.Refresh();
                    return;
                }

                if (MessageBox.Show("Удалить позицию?\n\nМатериал будет автоматически возвращен на склад (резерв снят).", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _materials.Remove(item);
                    UpdateTotalAmount();
                    dgMaterials.Items.Refresh();
                }
            }
        }

        private void btnAddService_Click(object sender, RoutedEventArgs e)
        {
            if (cmbServiceSelect.SelectedItem is Услуги selectedSvc)
            {
                var newItem = new OrderServiceItem
                {
                    idУслуги = selectedSvc.idУслуга,
                    Наименование = selectedSvc.Наименование,
                    Количество = 1,
                    ЦенаЗаЕдиницу = selectedSvc.Стоимость ?? 0,
                    СтатусПозиции = true,
                    IsNew = true
                };
                _services.Add(newItem);
                UpdateTotalAmount();
                cmbServiceSelect.SelectedIndex = -1;
            }
        }

        private void btnRemoveService_Click(object sender, RoutedEventArgs e)
        {
            if (dgServices.SelectedItem is OrderServiceItem item)
            {
                _services.Remove(item);
                UpdateTotalAmount();
            }
        }

        /// <summary>
        /// Обработчик изменения статуса заказа
        /// При смене статуса на "Выдан клиенту" (6) материалы автоматически списываются
        /// При смене статуса на "Отменен" (7) материалы автоматически возвращаются
        /// </summary>
        private void cmbxStatusInCard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbxStatusInCard.SelectedValue == null)
                return;

            int newStatusId = (int)cmbxStatusInCard.SelectedValue;
            int currentStatusId = _currentOrder.idСтатус ?? 1;

            // Если статус не изменился, ничего не делаем
            if (newStatusId == currentStatusId)
                return;

            // Автоматическое списание материалов при смене статуса на "Выдан клиенту" (6)
            if (newStatusId == 6 && currentStatusId != 6)
            {
                try
                {
                    using (var service = new OrderService())
                    {
                        service.WriteOffMaterialsForDeliveredOrder(_currentOrder.idЗаказ, _materials.ToList());
                    }
                    MessageBox.Show("Заказ выдан клиенту. Все материалы списаны со склада.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при списании материалов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    cmbxStatusInCard.SelectedValue = currentStatusId;
                    return;
                }
            }

            // Автоматическая очистка резервов при отмене заказа (статус 7)
            if (newStatusId == 7 && currentStatusId != 7)
            {
                try
                {
                    using (var service = new OrderService())
                    {
                        // Снимаем резерв со всех материалов заказа
                        service.CancelOrderWithReturn(_currentOrder.idЗаказ, _materials.ToList());
                    }
                    MessageBox.Show("Заказ отменен. Все материалы возвращены на склад (резервы сняты).", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при возврате материалов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    cmbxStatusInCard.SelectedValue = currentStatusId;
                    return;
                }
            }

            // Обновляем текущий статус
            _currentOrder.idСтатус = newStatusId;
        }

        private void dgMaterials_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            UpdateTotalAmount();
        }

        private void dgServices_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            UpdateTotalAmount();
        }

        private void UpdateTotalAmount()
        {
            // Безопасное суммирование
            decimal matTotal = _materials.Sum(m => m.СтоимостьПозиции);
            decimal svcTotal = _services.Sum(s => s.СтоимостьПозиции);

            txtTotalCostFromStructure.Text = (matTotal + svcTotal).ToString("F2");
            txtOrderPrepayment.Text = (matTotal).ToString("F2");
            txtOrderPrice.Text = (matTotal + svcTotal).ToString("F2");
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentOrder.Клиент.Фамилия)
                || string.IsNullOrWhiteSpace(_currentOrder.Клиент.Имя)
                || string.IsNullOrWhiteSpace(_currentOrder.Клиент.Отчество))
            {
                MessageBox.Show("Заполните ФИО клиента.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_currentOrder.Клиент.НомерТелефона) && string.IsNullOrWhiteSpace(_currentOrder.Клиент.Email))
            {
                MessageBox.Show("Заполните номер телефона клиента или его адрес электронной почты.");
                return;
            }
            if (cmbxTechnician.SelectedItem == null)
            {
                MessageBox.Show("Выберете технического специалиста.");
                return;
            }

            var materialsToCheck = _materials.Where(m => m.idСтатус == 8).ToList();
            if (materialsToCheck.Count > 0)
            {
                var result = _orderService.CheckAvailability(materialsToCheck);
                if (!result.IsAvailable)
                {
                    string errorMsg = "Недостаточно материалов на складе:\n";
                    foreach (var err in result.Errors)
                    {
                        errorMsg += "\n" + err;
                    }
                    MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Принудительно обновляем свойства из элементов управления
            _currentOrder.idСтатус = (int?)cmbxStatusInCard.SelectedValue ?? _currentOrder.idСтатус;
            _currentOrder.idТипУстройства = (int?)cmbxTypeDevice.SelectedValue ?? _currentOrder.idТипУстройства;

            // Сохранение техника
            if (_isNewOrder && AuthService.IsTechnician && AuthService.CurrentUserId.HasValue)
            {
                // Для нового заказа техника всегда подставляем текущего авторизованного техника
                _currentOrder.idПользователь = AuthService.CurrentUserId.Value;
            }
            else if (cmbxTechnician.SelectedValue != null)
            {
                _currentOrder.idПользователь = (int)cmbxTechnician.SelectedValue;
            }
            else
            {
                _currentOrder.idПользователь = null;
            }

            // Принудительно обновляем текстовые поля (на случай если binding не сработал)
            _currentOrder.ИмяУстройства = txtModel.Text?.Trim();
            _currentOrder.СерийныйНомер = txtSerialNumber.Text?.Trim();
            _currentOrder.Неисправность = txtMalfunction.Text?.Trim();

            // Обновляем данные клиента
            if (_currentOrder.Клиент == null)
                _currentOrder.Клиент = new Клиент();

            _currentOrder.Клиент.Фамилия = txtFIOClient_1.Text?.Trim();
            _currentOrder.Клиент.Имя = txtFIOClient_2.Text?.Trim();
            _currentOrder.Клиент.Отчество = txtFIOClient_3.Text?.Trim();
            _currentOrder.Клиент.НомерТелефона = txtPhoneNumber.Text?.Trim();

            try
            {
                using (var service = new OrderService())
                {
                    service.SaveOrder(_currentOrder, _materials.ToList(), _services.ToList(), _isNewOrder);
                }

                MessageBox.Show("Заказ сохранен успешно!");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void txtModel_TextChanged(object sender, TextChangedEventArgs e)
        {
            //if (txtModel.Text != null)
            //{
            //    var trimmed = txtModel.Text.Trim();
            //    if (txtModel.Text != trimmed)
            //    {
            //        int caretIndex = txtModel.CaretIndex;
            //        txtModel.Text = trimmed;
            //        txtModel.CaretIndex = Math.Min(caretIndex, txtModel.Text.Length);
            //    }
            //}
        }

        private void txtSerialNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSerialNumber.Text != null)
            {
                var trimmed = txtSerialNumber.Text.Trim();
                if (txtSerialNumber.Text != trimmed)
                {
                    int caretIndex = txtSerialNumber.CaretIndex;
                    txtSerialNumber.Text = trimmed;
                    txtSerialNumber.CaretIndex = Math.Min(caretIndex, txtSerialNumber.Text.Length);
                }
            }
        }

        private void btnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_isNewOrder)
            {
                DialogResult = false;
                Close();
                return;
            }

            if (MessageBox.Show("Вы уверены, что хотите удалить этот заказ? Это действие нельзя отменить.", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var service = new OrderService())
                    {
                        service.DeleteOrder(_currentOrder.idЗаказ);
                    }
                    MessageBox.Show("Заказ удален.");
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Печать чека/накладной в формате HTML
        /// </summary>
        private void btnPrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (_isNewOrder || _currentOrder.idЗаказ <= 0)
            {
                MessageBox.Show("Сначала сохраните заказ.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var docService = new DocumentService())
                {
                    string path = docService.GenerateInvoiceHtml(_currentOrder);
                    System.Diagnostics.Process.Start(path);
                }

                MessageBox.Show($"Чек сформирован и открыт в браузере.", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _dictContext.Dispose();
            base.OnClosed(e);
        }
    }
}