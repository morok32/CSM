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
    public partial class MaterialsControl : UserControl
    {
        private ICollectionView _materialStockView;
        private List<object> _fullCombinedData;
        private Материал _currentMaterial;
        private bool _isEditing;

        public MaterialsControl()
        {
            InitializeComponent();
            LoadData();
            LoadMaterialTypes();
        }

        private void LoadMaterialTypes()
        {
            var context = ComputerServiceManagerEntities.GetContext();
            var materialTypes = context.ТипМатериала.ToList();
            materialTypes.Insert(0, new ТипМатериала { idТипМатериала = -1, Наименование = "Выберите тип" });
            cmbxTypeMaterial.ItemsSource = materialTypes;
        }

        public void LoadData()
        {
            var context = ComputerServiceManagerEntities.GetContext();

            var combinedData = context.Склад
                .AsNoTracking()
                .Include(s => s.Материал.ТипМатериала)
                .Select(s => new
                {
                    IdОстатка = s.idСклад,
                    Количество = s.Количество ?? 0,
                    НаименованиеМатериала = s.Материал.Наименование ?? "Не указано",
                    БазоваяСтоимость = s.Материал.БазоваяСтоимость ?? 0,
                    НаименованиеТипа = s.Материал.ТипМатериала.Наименование ?? "Не указан",
                   //ДатаДобавления = s.Материал.ДатаДобавления,
                    IdМатериала = s.Материал.idМатериал,
                    РозничнаяЦена = s.Материал.РозничнаяЦена,
                    Описание = s.Материал.Описание,
                    //СерийныйНомер = s.Материал.СерийныйНомер
                })
                .ToList();

            // Рассчитываем доступное количество для каждого материала
            var resultWithAvailable = combinedData.Select(item =>
            {
                int materialId = item.IdМатериала;
                decimal physicalQuantity = item.Количество;

                // Считаем сумму всех активных резервов (idСтатус=8) по этому материалу
                decimal totalReserved = context.СоставЗаказа_Материалы
                    .Where(m => m.idМатериал == materialId && m.idСтатус == 8)
                    .Sum(m => (decimal?)m.Количество) ?? 0;

                // Доступно = физическое - зарезервировано
                decimal available = physicalQuantity - totalReserved;

                return new
                {
                    item.IdОстатка,
                    item.Количество,
                    item.НаименованиеМатериала,
                    item.БазоваяСтоимость,
                    item.НаименованиеТипа,
                    item.IdМатериала,
                    item.РозничнаяЦена,
                    item.Описание,
                    Доступно = available
                };
            }).ToList();

            _fullCombinedData = resultWithAvailable.Cast<object>().ToList();
            _materialStockView = CollectionViewSource.GetDefaultView(_fullCombinedData);
            _materialStockView.Filter = FilterMaterialStock;
            MaterialsDataGrid.ItemsSource = _materialStockView;
        }


        private bool FilterMaterialStock(object item)
        {
            var row = item as dynamic;
            if (row == null) return false;

            if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                var searchText = searchTextBox.Text.ToLower();
                if (!row.НаименованиеМатериала.ToString().ToLower().Contains(searchText))
                    return false;
            }

            var selectedType = cmbFilterTypeMaterial.SelectedItem as ТипМатериала;
            if (selectedType != null && selectedType.idТипМатериала != -1)
            {
                if (selectedType.Наименование != row.НаименованиеТипа.ToString())
                    return false;
            }

            if (dateFromDatePickerForMaterials.SelectedDate.HasValue)
            {
                DateTime selectedDate = dateFromDatePickerForMaterials.SelectedDate.Value.Date;
                var materialAddedDate = row.ДатаДобавления;
                if (materialAddedDate == null || ((DateTime?)materialAddedDate)?.Date != selectedDate)
                    return false;
            }

            return true;
        }

        private void OnFilterChanged()
        {
            _materialStockView?.Refresh();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void cmbFilterTypeMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void btnResetDatePicker_Click(object sender, RoutedEventArgs e)
        {
            dateFromDatePickerForMaterials.SelectedDate = null;
            OnFilterChanged();
        }

        private void buttonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (MaterialsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите материал для редактирования");
                return;
            }

            var selectedRow = MaterialsDataGrid.SelectedItem as dynamic;
            int materialId = (int)selectedRow.IdМатериала;

            using (var context = new ComputerServiceManagerEntities())
            {
                _currentMaterial = context.Материал
                    .Include("ТипМатериала")
                    .Include("Склад")
                    .FirstOrDefault(m => m.idМатериал == materialId);

                if (_currentMaterial != null)
                {
                    cmbxTypeMaterial.SelectedValue = _currentMaterial.idТипМатериала;
                    txtModel.Text = _currentMaterial.Наименование;
                    //txtSerialNumber.Text = _currentMaterial.СерийныйНомер;
                    datePickerDateAdded.SelectedDate = _currentMaterial.ДатаДобавления;
                    txtQuantity.Text = _currentMaterial.Склад.FirstOrDefault()?.Количество.ToString() ?? "0";
                    txtBasePrice.Text = _currentMaterial.БазоваяСтоимость?.ToString() ?? "";
                    txtRetailPrice.Text = _currentMaterial.РозничнаяЦена?.ToString() ?? "";
                    txtDescription.Text = _currentMaterial.Описание;

                    _isEditing = true;
                    buttonSave.IsEnabled = true;
                    mainTabControl.SelectedItem = tabEditMaterials;
                }
            }
        }

        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            _currentMaterial = new Материал
            {
                ДатаДобавления = DateTime.Now
            };
            _currentMaterial.Склад.Add(new Склад { Количество = 0 });

            cmbxTypeMaterial.SelectedIndex = 0;
            txtModel.Text = "";
            txtSerialNumber.Text = "";
            datePickerDateAdded.SelectedDate = DateTime.Now;
            txtQuantity.Text = "0";
            txtBasePrice.Text = "";
            txtRetailPrice.Text = "";
            txtDescription.Text = "";

            _isEditing = true;
            buttonSave.Visibility = Visibility.Visible;
            mainTabControl.SelectedItem = tabEditMaterials;
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            if (cmbxTypeMaterial.SelectedItem == null || (int)cmbxTypeMaterial.SelectedValue == -1)
                errors.AppendLine("- Выберите тип материала.");
            if (string.IsNullOrWhiteSpace(txtModel.Text))
                errors.AppendLine("- Укажите наименование материала.");
            if (string.IsNullOrWhiteSpace(txtDescription.Text))
                errors.AppendLine("- Укажите описание материала.");

            if (errors.Length > 0)
            {
                MessageBox.Show($"Исправьте ошибки:\n{errors}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _currentMaterial.idТипМатериала = (int)cmbxTypeMaterial.SelectedValue;
                _currentMaterial.Наименование = txtModel.Text;
                //_currentMaterial.СерийныйНомер = txtSerialNumber.Text;
                _currentMaterial.ДатаДобавления = datePickerDateAdded.SelectedDate ?? DateTime.Now;
                _currentMaterial.Описание = txtDescription.Text;

                if (decimal.TryParse(txtBasePrice.Text, out decimal basePrice))
                    _currentMaterial.БазоваяСтоимость = basePrice;
                if (decimal.TryParse(txtRetailPrice.Text, out decimal retailPrice))
                    _currentMaterial.РозничнаяЦена = retailPrice;

                var stock = _currentMaterial.Склад.FirstOrDefault();
                if (stock == null)
                {
                    stock = new Склад { idМатериал = _currentMaterial.idМатериал };
                    _currentMaterial.Склад.Add(stock);
                }

                if (decimal.TryParse(txtQuantity.Text, out decimal quantity))
                    stock.Количество = quantity;

                using (var context = new ComputerServiceManagerEntities())
                {
                    if (_currentMaterial.idМатериал == 0)
                        context.Материал.Add(_currentMaterial);
                    else
                        context.Entry(_currentMaterial).State = EntityState.Modified;

                    context.SaveChanges();
                }

                LoadData();
                ClearForm();
                MessageBox.Show("Материал сохранен успешно", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MaterialsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите материал для удаления");
                return;
            }

            var selectedRow = MaterialsDataGrid.SelectedItem as dynamic;
            int materialId = (int)selectedRow.IdМатериала;

            if (MessageBox.Show("Удалить материал?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var context = new ComputerServiceManagerEntities())
                {
                    var material = context.Материал
                        .Include("Склад")
                        .FirstOrDefault(m => m.idМатериал == materialId);

                    if (material != null)
                    {
                        context.Склад.RemoveRange(material.Склад);
                        context.Материал.Remove(material);
                        context.SaveChanges();

                        LoadData();
                        if (_currentMaterial?.idМатериал == materialId)
                            ClearForm();
                    }
                }
            }
        }

        private void ClearForm()
        {
            _currentMaterial = null;
            _isEditing = false;
            buttonSave.Visibility = Visibility.Collapsed;
            mainTabControl.SelectedItem = tabDataGridForMaterials;
        }

        private void MaterialsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isEditing && mainTabControl.SelectedItem == tabEditMaterials)
            {
                if (MessageBox.Show("Закрыть форму редактирования?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    MaterialsDataGrid.SelectedItem = null;
                }
            }
        }
    }
}