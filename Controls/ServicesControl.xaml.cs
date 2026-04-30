using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ComputerServiceManager.Windows;
using System.Data.Entity;

namespace ComputerServiceManager.Controls
{
    public partial class ServicesControl : UserControl
    {
        private ICollectionView _servicesView;
        private List<object> _fullCombinedData;
        private Услуги _currentService;
        private bool _isEditing;

        public ServicesControl()
        {
            InitializeComponent();
            LoadData();
        }

        public void LoadData()
        {
            var context = ComputerServiceManagerEntities.GetContext();

            // Используем проекцию для согласованности с другими контролами
            var servicesData = context.Услуги
                .AsNoTracking()
                .Select(s => new
                {
                    s.idУслуга,
                    s.Наименование,
                    s.Стоимость,
                    s.Описание
                })
                .ToList();

            _fullCombinedData = servicesData.Cast<object>().ToList();
            _servicesView = CollectionViewSource.GetDefaultView(_fullCombinedData);
            _servicesView.Filter = FilterServices;
            ServicesDataGrid.ItemsSource = _servicesView;
        }

        private bool FilterServices(object item)
        {
            var row = item as dynamic;
            if (row == null) return false;

            if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                var searchText = searchTextBox.Text.ToLower();
                // Поиск по наименованию и описанию
                bool matchesName = row.Наименование?.ToString().ToLower().Contains(searchText) == true;
                bool matchesDescription = row.Описание?.ToString().ToLower().Contains(searchText) == true;

                if (!matchesName && !matchesDescription)
                    return false;
            }

            return true;
        }

        private void OnFilterChanged()
        {
            _servicesView?.Refresh();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void buttonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите услугу для редактирования");
                return;
            }

            var selectedRow = ServicesDataGrid.SelectedItem as dynamic;
            int serviceId = (int)selectedRow.idУслуга;

            using (var context = new ComputerServiceManagerEntities())
            {
                _currentService = context.Услуги
                    .FirstOrDefault(s => s.idУслуга == serviceId);

                if (_currentService != null)
                {
                    // Обнуляем navigation properties, если они есть
                    // В данном случае, предполагаем, что нет связанных сущностей,
                    // но оставляем шаблон для будущих изменений
                    PrepareEntityForSave(_currentService);

                    txtServiceName.Text = _currentService.Наименование;
                    txtServicePrice.Text = _currentService.Стоимость?.ToString() ?? "";
                    txtDescription.Text = _currentService.Описание;

                    _isEditing = true;
                    buttonSave.IsEnabled = true;
                    mainTabControl.SelectedItem = tabEditServices;
                }
            }
        }

        private void PrepareEntityForSave(Услуги service)
        {
            // Обнуляем navigation properties для предотвращения конфликтов
            // В текущей реализации, возможно, не требуется, но добавлено для единообразия
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(txtServiceName.Text))
                errors.AppendLine("- Укажите наименование услуги.");
            if (string.IsNullOrWhiteSpace(txtServicePrice.Text))
                errors.AppendLine("- Укажите стоимость услуги.");

            if (errors.Length > 0)
            {
                MessageBox.Show($"Исправьте ошибки:\n{errors}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_currentService == null)
                    _currentService = new Услуги();

                _currentService.Наименование = txtServiceName.Text;

                if (decimal.TryParse(txtServicePrice.Text, out decimal price))
                    _currentService.Стоимость = price;

                _currentService.Описание = txtDescription.Text;

                // Обнуляем navigation properties перед сохранением
                PrepareEntityForSave(_currentService);

                using (var context = new ComputerServiceManagerEntities())
                {
                    if (_currentService.idУслуга == 0)
                    {
                        context.Услуги.Add(_currentService);
                    }
                    else
                    {
                        context.Entry(_currentService).State = EntityState.Modified;
                    }

                    context.SaveChanges();
                }

                LoadData();
                ClearForm();
                MessageBox.Show("Услуга сохранена успешно", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите услугу для удаления");
                return;
            }

            var selectedRow = ServicesDataGrid.SelectedItem as dynamic;
            int serviceId = (int)selectedRow.idУслуга;

            if (MessageBox.Show("Удалить услугу?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var context = new ComputerServiceManagerEntities())
                {
                    var service = context.Услуги
                        .FirstOrDefault(s => s.idУслуга == serviceId);

                    if (service != null)
                    {
                        context.Услуги.Remove(service);
                        context.SaveChanges();

                        LoadData();
                        if (_currentService?.idУслуга == serviceId)
                            ClearForm();
                    }
                }
            }
        }

        private void ClearForm()
        {
            _currentService = null;
            _isEditing = false;
            buttonSave.IsEnabled = true;
            mainTabControl.SelectedItem = tabDataGridForServices;
            txtServiceName.Text = "";
            txtServicePrice.Text = "";
            txtDescription.Text = "";
        }

        private void ServicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isEditing && mainTabControl.SelectedItem == tabEditServices)
            {
                if (MessageBox.Show("Закрыть форму редактирования?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    ServicesDataGrid.SelectedItem = null;
                }
            }
        }

        private void buttonClean_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }
    }
}