using ComputerServiceManager.Models;
using ComputerServiceManager.Services;
using ComputerServiceManager.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public OrderEditWindow(Заказ order)
        {
            InitializeComponent();
            _isNewOrder = order == null;
            _dictContext = new ComputerServiceManagerEntities();

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
                using (var svc = new OrderService())
                {
                    var items = svc.GetOrderItems(order.idЗаказ);
                    _materials = new ObservableCollection<OrderMaterialItem>(items.Materials);
                    _services = new ObservableCollection<OrderServiceItem>(items.Services);
                }
            }

            DataContext = _currentOrder;
            dgMaterials.ItemsSource = _materials;
            dgServices.ItemsSource = _services;

            LoadDictionaries();
            UpdateTotalAmount();
        }

        private void LoadDictionaries()
        {
            cmbxStatusInCard.ItemsSource = _dictContext.Статус.ToList();
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

            cmbxTechnician.ItemsSource = _dictContext.Пользователь.Where(p => p.Активность == true).ToList();
            cmbxTechnician.SelectedValuePath = "idПользователь";

            var materialsList = _dictContext.Материал.ToList();
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
                // Проверка: нельзя удалить списанный материал (idСтатус == 9)
                if (item.idСтатус == 9)
                {
                    MessageBox.Show("Нельзя удалить списанный материал. Сначала необходимо выполнить возврат на склад.", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Удаляем позицию из списка (резерв автоматически снимается)
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
        /// Списание материала со склада (физическое уменьшение количества)
        /// При добавлении в заказ материал уже зарезервирован (idСтатус = 8 "Резерв")
        /// По нажатию "Списать" уменьшается физическое количество на складе и статус меняется на "Списан" (idСтатус = 9)
        /// </summary>
        private void btnWriteOff_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный элемент из DataGrid
            if (dgMaterials.SelectedItem is OrderMaterialItem item)
            {
                try
                {
                    using (var context = new ComputerServiceManagerEntities())
                    {
                        var warehouseSvc = new WarehouseService(context);
                        decimal available = warehouseSvc.GetAvailableQuantity(item.idМатериала);
                        decimal physical = warehouseSvc.GetPhysicalQuantity(item.idМатериала);

                        // Проверка: если материал уже списан
                        if (item.idСтатус == 9)
                        {
                            MessageBox.Show($"Материал '{item.Наименование}' уже списан со склада.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        // Проверка статуса заказа - списание разрешено только для заказов со статусом "Выдан клиенту" (id=6)
                        int currentOrderStatus = _currentOrder.idСтатус ?? 1;
                        if (currentOrderStatus != 6)
                        {
                            MessageBox.Show("Списание разрешено только у заказов которые имеют статус 'Выдан клиенту'", 
                                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Проверка доступности материала
                        if (available < item.Количество)
                        {
                            decimal? reservedNullable = context.СоставЗаказа_Материалы
                                .Where(m => m.idМатериал == item.idМатериала && (m.idСтатус == 8))
                                .Sum(m => (decimal?)m.Количество);
                            decimal reserved = reservedNullable ?? 0;

                            MessageBox.Show($"Недостаточно материала '{item.Наименование}'. " +
                                $"Доступно: {available}, требуется: {item.Количество}\n" +
                                $"(на складе: {physical}, зарезервировано другими заказами: {reserved})",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Предупреждение о невозможности возврата
                        var confirmResult = MessageBox.Show(
                            "Внимание! После списания позиция не сможет быть возвращена на склад.\n\nПродолжить?",
                            "Подтверждение списания",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (confirmResult != MessageBoxResult.Yes)
                        {
                            return;
                        }

                        // Находим позицию в БД и выполняем физическое списание
                        if (!item.IsNew && item.idПозиции > 0)
                        {
                            var entity = context.СоставЗаказа_Материалы.Find(item.idПозиции);
                            if (entity != null)
                            {
                                // Уменьшаем физическое количество на складе
                                var stock = context.Склад.FirstOrDefault(s => s.idМатериал == entity.idМатериал);
                                if (stock != null)
                                {
                                    if (stock.Количество >= entity.Количество)
                                    {
                                        stock.Количество -= entity.Количество;
                                        
                                        // Обновляем статус на "Списан" (id=9)
                                        entity.idСтатус = 9;
                                        context.SaveChanges();

                                        // Обновляем статус в UI
                                        item.idСтатус = 9;
                                        
                                        dgMaterials.Items.Refresh();
                                        UpdateTotalAmount();

                                        MessageBox.Show($"Материал '{item.Наименование}' списан со склада!\nФизическое количество уменьшено на {item.Количество}.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Недостаточно материала '{item.Наименование}' на складе. Доступно: {stock.Количество}, требуется: {entity.Количество}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Сначала сохраните заказ, затем списывайте материалы.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при списании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите материал для списания.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Проверка возможности смены статуса заказа
        /// </summary>
        private bool CanChangeStatus(int newStatusId, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Статусы: 1-Новый, 2-Диагностика, 3-Ожидает запчастей, 4-В работе, 5-Готов, 6-Отдан, 7-Отменен
            int currentStatusId = _currentOrder.idСтатус ?? 1;

            // Проверяем есть ли материалы у которых idСтатус = 9 (физически списаны)
            var hasWrittenOffMaterials = _materials.Any(m => m.idСтатус == 9);

            // Логика 1.1: Смена с (4,5,6) на (1,2,3) - если есть списанные материалы, нужно сначала вернуть
            if ((currentStatusId == 4 || currentStatusId == 5 || currentStatusId == 6) &&
                (newStatusId == 1 || newStatusId == 2 || newStatusId == 3))
            {
                if (hasWrittenOffMaterials)
                {
                    errorMessage = "Сначала нужно вернуть все списанные материалы на склад! Используйте кнопку 'Вернуть' в списке материалов.";
                    return false;
                }
            }

            // Логика 1.2: Смена с (1,2,3) на (4,5,6) - если нет списанных материалов, нужно сначала списать
            if ((currentStatusId == 1 || currentStatusId == 2 || currentStatusId == 3) &&
                (newStatusId == 4 || newStatusId == 5 || newStatusId == 6))
            {
                if (!hasWrittenOffMaterials)
                {
                    errorMessage = "Сначала нужно списать материалы со склада! Используйте кнопку 'Списать' в списке материалов.";
                    return false;
                }
            }

            // Логика 1.3: Смена с 7 (Отменен) на любой другой - если есть списанные материалы, нужно сначала вернуть
            if (currentStatusId == 7 && newStatusId != 7)
            {
                if (hasWrittenOffMaterials)
                {
                    errorMessage = "Сначала нужно вернуть все списанные материалы на склад! Используйте кнопку 'Вернуть' в списке материалов.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Обработчик изменения статуса заказа
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

            // Проверяем возможность смены статуса
            if (!CanChangeStatus(newStatusId, out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Возвращаем старый статус в ComboBox
                cmbxStatusInCard.SelectedValue = currentStatusId;
                return;
            }

            // РЕШЕНИЕ ПРОБЛЕМЫ №2: Автоматическая очистка резервов при отмене заказа (статус 7)
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
            txtOrderPrice.Text = (matTotal + svcTotal).ToString("F2");
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentOrder.Клиент.Фамилия))
            {
                MessageBox.Show("Заполните Фамилию клиента.");
                return;
            }

            var materialsToCheck = _materials.Where(m => m.idСтатус == 8 || m.IsReserved).ToList();
            if (materialsToCheck.Count > 0)
            {
                using (var context = new ComputerServiceManagerEntities())
                {
                    var warehouseSvc = new WarehouseService(context);
                    var result = warehouseSvc.CheckAvailability(materialsToCheck);
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
            }

            // Принудительно обновляем свойства из элементов управления
            _currentOrder.idСтатус = (int?)cmbxStatusInCard.SelectedValue ?? _currentOrder.idСтатус;
            _currentOrder.idТипУстройства = (int?)cmbxTypeDevice.SelectedValue ?? _currentOrder.idТипУстройства;

            // Сохранение техника
            if (cmbxTechnician.SelectedValue != null)
                _currentOrder.idПользователь = (int)cmbxTechnician.SelectedValue;
            else
                _currentOrder.idПользователь = null;

            // Принудительно обновляем текстовые поля (на случай если binding не сработал)
            _currentOrder.ИмяУстройства = txtModel.Text;
            _currentOrder.СерийныйНомер = txtSerialNumber.Text;
            _currentOrder.Неисправность = txtMalfunction.Text;

            // Обновляем данные клиента
            if (_currentOrder.Клиент == null)
                _currentOrder.Клиент = new Клиент();

            _currentOrder.Клиент.Фамилия = txtFIOClient_1.Text;
            _currentOrder.Клиент.Имя = txtFIOClient_2.Text;
            _currentOrder.Клиент.Отчество = txtFIOClient_3.Text;
            _currentOrder.Клиент.НомерТелефона = txtPhoneNumber.Text;

            try
            {
                using (var service = new OrderService())
                {
                    service.SaveOrder(_currentOrder, _materials.ToList(), _services.ToList(), _isNewOrder);
                }

                MessageBox.Show("Заказ сохранен успешно!");
                //DialogResult = true;
                //Close();
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

        protected override void OnClosed(EventArgs e)
        {
            _dictContext.Dispose();
            base.OnClosed(e);
        }
    }
}