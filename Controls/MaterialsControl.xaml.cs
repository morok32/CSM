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
        private bool _isTechnicianMode;  // Флаг режима техника
        private decimal _globalVatPercent = 5m; // Глобальный процент НДС
        private decimal _globalMarkupPercent = 0m; // Глобальный процент наценки

        public MaterialsControl()
        {
            InitializeComponent();
            LoadData();
            LoadMaterialTypes();
        }

        /// <summary>
        /// Устанавливает режим техника (ограничивает доступ к редактированию)
        /// </summary>
        /// <param name="isTechnician">true если пользователь - техник</param>
        public void SetTechnicianMode(bool isTechnician)
        {
            _isTechnicianMode = isTechnician;

            if (_isTechnicianMode)
            {
                // Блокируем вкладку "Редактирование"
                if (tabEditMaterials != null)
                {
                    tabEditMaterials.IsEnabled = false;
                }

                // Блокируем кнопки: Открыть, Сохранить, Удалить, Очистить
                if (buttonEdit != null) buttonEdit.IsEnabled = false;
                if (buttonSave != null) buttonSave.IsEnabled = false;
                if (buttonDelete != null) buttonDelete.IsEnabled = false;
                if (buttonClean != null) buttonClean.IsEnabled = false;
            }
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

            // Исправленный запрос с уникальными именами свойств
            var stockData = context.Склад
                .GroupJoin(
                    context.СоставЗаказа_Материалы.Where(m => m.idСтатус == 8),
                    s => s.idМатериал,
                    m => m.idМатериал,
                    (s, materials) => new
                    {
                        s.idСклад,
                        s.Количество,
                        s.Материал.idМатериал,
                        MaterialName = s.Материал.Наименование,  // Явное имя для избежания конфликта
                        s.Материал.БазоваяСтоимость,
                        TypeName = s.Материал.ТипМатериала.Наименование,  // Явное имя для избежания конфликта
                        s.Материал.РозничнаяЦена,
                        s.Материал.Описание,
                        Reserved = materials.Sum(m => m.Количество) ?? 0
                    })
                .ToList();

            var resultWithAvailable = stockData.Select(item => new
            {
                IdОстатка = item.idСклад,
                Количество = item.Количество ?? 0,
                НаименованиеМатериала = item.MaterialName ?? "Не указано",  // Используем новое имя
                БазоваяСтоимость = item.БазоваяСтоимость ?? 0,
                НаименованиеТипа = item.TypeName ?? "Не указан",  // Используем новое имя
                IdМатериала = item.idМатериал,
                РозничнаяЦена = item.РозничнаяЦена,
                Описание = item.Описание,
                Доступно = item.Количество - item.Reserved
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

            // Поиск по названию материала
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

            // Фильтр по минимальной цене
            if (!string.IsNullOrWhiteSpace(txtMinPrice.Text))
            {
                if (decimal.TryParse(txtMinPrice.Text, out decimal minPrice))
                {
                    if (row.РозничнаяЦена < minPrice)
                        return false;
                }
            }

            // Фильтр по максимальной цене
            if (!string.IsNullOrWhiteSpace(txtMaxPrice.Text))
            {
                if (decimal.TryParse(txtMaxPrice.Text, out decimal maxPrice))
                {
                    if (row.РозничнаяЦена > maxPrice)
                        return false;
                }
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

        private void txtMinPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void txtMaxPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void buttonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isTechnicianMode)
            {
                MessageBox.Show("У вас нет прав на редактирование материалов", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                    .FirstOrDefault(m => m.idМатериал == materialId);

                // Обнуляем navigation properties для предотвращения конфликтов
                if (_currentMaterial != null)
                {
                    _currentMaterial.ТипМатериала = null;
                    _currentMaterial.Склад.ToList().ForEach(s => s.Материал = null);
                }

                if (_currentMaterial != null)
                {
                    cmbxTypeMaterial.SelectedValue = _currentMaterial.idТипМатериала;
                    txtModel.Text = _currentMaterial.Наименование;
                    datePickerDateAdded.SelectedDate = _currentMaterial.ДатаДобавления;
                    txtQuantity.Text = _currentMaterial.Склад.FirstOrDefault()?.Количество.ToString() ?? "0";
                    txtBasePrice.Text = _currentMaterial.БазоваяСтоимость?.ToString() ?? "";
                    txtMarkupPercent.Text = _globalMarkupPercent.ToString("F2");
                    txtRetailPrice.Text = CalculateRetailPriceFromBaseAndMarkup(_currentMaterial.БазоваяСтоимость ?? 0);

                    txtDescription.Text = _currentMaterial.Описание;

                    _isEditing = true;
                    buttonSave.IsEnabled = true;
                    mainTabControl.SelectedItem = tabEditMaterials;
                }
            }
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isTechnicianMode)
            {
                MessageBox.Show("У вас нет прав на сохранение материалов", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                if (_currentMaterial == null)
                {
                    _currentMaterial = new Материал();
                    _currentMaterial.Склад = new ObservableCollection<Склад>();
                }

                _currentMaterial.idТипМатериала = (int)cmbxTypeMaterial.SelectedValue;
                _currentMaterial.Наименование = txtModel.Text;
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

                // Обнуляем navigation properties для предотвращения конфликтов
                PrepareEntityForSave(_currentMaterial);

                using (var context = new ComputerServiceManagerEntities())
                {
                    if (_currentMaterial.idМатериал == 0)
                    {
                        context.Материал.Add(_currentMaterial);
                    }
                    else
                    {
                        context.Entry(_currentMaterial).State = EntityState.Modified;
                    }

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

        private void PrepareEntityForSave(Материал material)
        {
            // Обнуляем navigation properties, чтобы избежать конфликтов при сохранении
            material.ТипМатериала = null;

            if (material.Склад != null)
            {
                foreach (var stock in material.Склад.ToList())
                {
                    stock.Материал = null;
                }
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_isTechnicianMode)
            {
                MessageBox.Show("У вас нет прав на удаление материалов", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                        .FirstOrDefault(m => m.idМатериал == materialId);

                    if (material != null)
                    {
                        // Сначала удаляем записи из Склад
                        var stocks = context.Склад.Where(s => s.idМатериал == materialId).ToList();
                        context.Склад.RemoveRange(stocks);

                        // Затем удаляем сам материал
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
            buttonSave.IsEnabled = true;
            mainTabControl.SelectedItem = tabDataGridForMaterials;

            cmbxTypeMaterial.SelectedIndex = 0;
            txtModel.Text = "";
            datePickerDateAdded.SelectedDate = DateTime.Now;
            txtQuantity.Text = "0";
            txtBasePrice.Text = "";
            txtMarkupPercent.Text = "0";
            txtRetailPrice.Text = "";
            txtDescription.Text = "";
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

        private void buttonClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isTechnicianMode)
            {
                MessageBox.Show("У вас нет прав на очистку формы", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ClearForm();
        }

        /// <summary>
        /// Рассчитывает розничную цену на основе базовой цены и глобальной наценки с учетом НДС
        /// </summary>
        private string CalculateRetailPriceFromBaseAndMarkup(decimal basePrice)
        {
            decimal priceWithMarkup = basePrice * (1 + _globalMarkupPercent / 100);
            decimal retailPrice = priceWithMarkup * (1 + _globalVatPercent / 100);
            return retailPrice.ToString("F2");
        }

        private void CalculateRetailPrice()
        {
            if (txtBasePrice == null || txtMarkupPercent == null || txtRetailPrice == null)
                return;

            if (decimal.TryParse(txtBasePrice.Text, out decimal basePrice) &&
                decimal.TryParse(txtMarkupPercent.Text, out decimal markupPercent))
            {
                // Сначала рассчитываем цену с наценкой, затем добавляем НДС
                decimal priceWithMarkup = basePrice * (1 + markupPercent / 100);
                decimal retailPrice = priceWithMarkup * (1 + _globalVatPercent / 100);
                txtRetailPrice.Text = retailPrice.ToString("F2");
            }
            else
            {
                txtRetailPrice.Text = string.Empty;
            }
        }

        private void txtMarkupPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(txtMarkupPercent.Text, out decimal markupPercent))
            {
                _globalMarkupPercent = markupPercent;
            }
            CalculateRetailPrice();
        }

        private void txtBasePrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateRetailPrice();
        }
    }
}