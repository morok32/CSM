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
        private List<Услуги> _fullServicesData;
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

            _fullServicesData = context.Услуги
                .AsNoTracking()
                .ToList();

            _servicesView = CollectionViewSource.GetDefaultView(_fullServicesData);
            _servicesView.Filter = FilterServices;
            ServicesDataGrid.ItemsSource = _servicesView;
        }

        private bool FilterServices(object item)
        {
            var service = item as Услуги;
            if (service == null) return false;

            if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                var searchText = searchTextBox.Text.ToLower();
                // Поиск по наименованию и описанию
                bool matchesName = service.Наименование?.ToLower().Contains(searchText) == true;
                bool matchesDescription = service.Описание?.ToLower().Contains(searchText) == true;
                
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

        private void cmbFilterTypeMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

            _currentService = ServicesDataGrid.SelectedItem as Услуги;

            if (_currentService != null)
            {
                txtServiceName.Text = _currentService.Наименование;
                txtServicePrice.Text = _currentService.Стоимость?.ToString() ?? "";
                txtDescription.Text = _currentService.Описание;

                _isEditing = true;
                buttonSave.IsEnabled = true;
                mainTabControl.SelectedItem = tabEditServices;
            }
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
                _currentService.Наименование = txtServiceName.Text;

                if (decimal.TryParse(txtServicePrice.Text, out decimal price))
                    _currentService.Стоимость = price;

                _currentService.Описание = txtDescription.Text;

                using (var context = new ComputerServiceManagerEntities())
                {
                    if (_currentService.idУслуга == 0)
                        context.Услуги.Add(_currentService);
                    else
                        context.Entry(_currentService).State = EntityState.Modified;

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

            var selectedService = ServicesDataGrid.SelectedItem as Услуги;
            int serviceId = (int)selectedService.idУслуга;

            if (MessageBox.Show("Удалить услугу?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var context = new ComputerServiceManagerEntities())
                {
                    var service = context.Услуги.FirstOrDefault(s => s.idУслуга == serviceId);

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
            buttonSave.IsEnabled = false;
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
