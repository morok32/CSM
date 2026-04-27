using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity; // Убедитесь, что подключена библиотека Entity Framework
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
using ComputerServiceManager.Windows;

namespace ComputerServiceManager.Windows
{
    /// <summary>
    /// Логика взаимодействия для materialCardWindow.xaml
    /// </summary>
    public partial class materialCardWindow : Window
    {
        private ComputerServiceManagerEntities _context; // контекст (переменная с определенным типом)
        private Материал _currentMaterial; // выбранный материал (переменная с определенным типом)
        private bool _isNewMaterial; // новый материал (переменная с определенным типом)
        private Склад _currentStock;

        public materialCardWindow(Материал material = null)
        {
            InitializeComponent();

            _context = new ComputerServiceManagerEntities(); // заполненная переменная
            _isNewMaterial = material == null; // проверка, равен ли material null, если да то _isNewMaterial = true

            if (_isNewMaterial) // если _isNewMaterial = true
            {
                _currentMaterial = new Материал()
                {
                    ДатаДобавления = DateTime.Now,
                };
                
                // Создаем новый складской остаток для нового материала
                var newStock = new Склад()
                {
                    Количество = 0,
                    Материал = _currentMaterial
                };
                _currentMaterial.Склад.Add(newStock);
                _context.Материал.Add(_currentMaterial);
                _currentStock = newStock;
            }
            else
            {
                // загрузка существующего материала с навигационными свойствами
                int materialId = material.idМатериал;
                _currentMaterial = _context.Материал
                    .Include("ТипМатериала")
                    .Include("СкладскиеОстатки")
                    .FirstOrDefault(o => o.idМатериал == materialId);

                if (_currentMaterial == null)
                {
                    MessageBox.Show("Материал не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // Получаем существующий остаток, если он есть
                // Предполагаем связь 1:1 или берем первый, если их несколько (обычно 1:1)
                _currentStock = _currentMaterial.Склад.FirstOrDefault();

                // Если у материала нет записи об остатке, создаем её
                if (!_currentMaterial.Склад.Any())
                {
                    var newStock = new Склад()
                    {
                        Количество = 0,
                        Материал = _currentMaterial
                    };
                    _context.Склад.Add(newStock);
                    // Добавляем в коллекцию через Add, а не присваиванием
                    _currentMaterial.Склад.Add(newStock);
                    _currentStock = newStock;
                }
            }

            this.DataContext = _currentMaterial;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // загрузка справочников
                // указывает коллекцию объектов(список) из которого ComboBox будет брать данные
                cmbxTypeMaterial.ItemsSource = _context.ТипМатериала.ToList();
                // настройка отображения для ComboBox
                cmbxTypeMaterial.DisplayMemberPath = "Наименование";

                // если это редактирование, установим выбранные значения
                if (!_isNewMaterial)
                {
                    // значение которое будет загружено в ComboBox
                    cmbxTypeMaterial.SelectedValue = _currentMaterial.ТипМатериала.idТипМатериала; // Устанавливаем ID, а не объект
                    
                    // Устанавливаем значение количества из складского остатка
                    if (_currentStock != null)
                    {
                        txtQuantity.Text = _currentStock.Количество?.ToString() ?? "0";
                    }
                }
                else
                {
                    // Для нового материала устанавливаем 0
                    txtQuantity.Text = "0";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSaveOrderCardState_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_currentMaterial.Наименование))
                errors.AppendLine("- Укажите наименование материала.");
            if (string.IsNullOrWhiteSpace(_currentMaterial.Описание))
                errors.AppendLine("- Укажите описание материала.");
            if (cmbxTypeMaterial.SelectedItem == null)
                errors.AppendLine("- Выберите тип материала.");

            if (!string.IsNullOrEmpty(errors.ToString()))
            {
                MessageBox.Show($"Пожалуйста, исправьте следующие ошибки:\n{errors}", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // установка ID из выбранных элементов ComboBox
            _currentMaterial.idТипМатериала = (cmbxTypeMaterial.SelectedItem as ТипМатериала)?.idТипМатериала;

            // Обновляем количество из TextBox
            if (_currentStock != null)
            {
                decimal quantity;
                if (decimal.TryParse(txtQuantity.Text, out quantity))
                {
                    _currentStock.Количество = quantity;
                }
                else
                {
                    _currentStock.Количество = 0;
                }
            }

            try
            {
                if (_isNewMaterial && _currentStock != null)
                {
                    if (_context.Entry(_currentStock).State == EntityState.Detached)
                    {
                        _context.Склад.Add(_currentStock);
                    }
                }

                _context.SaveChanges();
                MessageBox.Show("Материал успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Вспомогательный метод для обновления состава заказов при изменении статуса материала
        private void UpdateOrderStructureForMaterialStatusChange(int materialId)
        {
            try
            {
                // Найти все записи в СоставЗаказа_Материалы для этого материала
                var materialEntriesInOrders = _context.СоставЗаказа_Материалы
                    .Where(me => me.idМатериал == materialId)
                    .ToList(); // Загружаем список для работы с ним

                if (materialEntriesInOrders.Any())
                {
                    // Удалить все эти записи из состава заказов
                    _context.СоставЗаказа_Материалы.RemoveRange(materialEntriesInOrders);
                    _context.SaveChanges(); // Сохраняем изменения в составе заказов
                    MessageBox.Show($"Материал был снят со всех заказов, так как его статус изменён на 'Не используется'.", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибки при обновлении состава заказов
                MessageBox.Show($"Ошибка при обновлении состава заказов для материала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                // В реальном приложении может потребоваться логгирование или откат транзакции
            }
        }


        public void btnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_isNewMaterial)
            {
                MessageBox.Show("Невозможно удалить несохраненный материал.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Вы уверены, что хотите удалить этот материал?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    // Сначала удалить связанные позиции материала в составе заказов
                    var materialEntriesInOrders = _context.СоставЗаказа_Материалы.Where(s => s.idМатериал == _currentMaterial.idМатериал).ToList();
                    _context.СоставЗаказа_Материалы.RemoveRange(materialEntriesInOrders);

                    var stockEntries = _context.Склад.Where(s => s.idМатериал == _currentMaterial.idМатериал).ToList();
                    _context.Склад.RemoveRange(stockEntries);

                    // Затем удалить сам материал
                    _context.Материал.Remove(_currentMaterial);
                    _context.SaveChanges();
                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _context?.Dispose();
            base.OnClosing(e);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Указывает, что операция была отменена
            this.Close();
        }
    }
}