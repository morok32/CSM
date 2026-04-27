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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ComputerServiceManager.Controls
{
    /// <summary>
    /// Логика взаимодействия для ServicesControl.xaml
    /// </summary>
    public partial class ServicesControl : UserControl
    {
        private ComputerServiceManagerEntities context;
        private Услуги editingService; // Текущая редактируемая услуга
        private bool isDirty = false; // Были ли изменения

        public ServicesControl()
        {
            InitializeComponent();
            LoadServices();
    
        }

        private void LoadServices()
        {
            using (context = new ComputerServiceManagerEntities())
            {
                ServicesDataGrid.ItemsSource = context.Услуги.ToList();
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            using (context = new ComputerServiceManagerEntities())
            {
                var query = context.Услуги.AsQueryable();

               

                ServicesDataGrid.ItemsSource = query.ToList();
            }
        }

        //private void ServicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (ServicesDataGrid.SelectedItem is Услуги selectedService)
        //    {
        //        txtServiceName.Text = selectedService.Наименование;
        //        txtServicePrice.Text = selectedService.Стоимость?.ToString() ?? string.Empty;
        //        editingService = selectedService;
        //    }
        //}

        private void StartEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesDataGrid.SelectedItem is Услуги selectedService)
            {
                if (selectedService != null)
                {
                    txtServiceName.Text = selectedService.Наименование;
                    txtServicePrice.Text = selectedService.Стоимость?.ToString() ?? string.Empty;
                    editingService = selectedService;
                }
                else
                {
                    MessageBox.Show("Выберите услугу для редактирования.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SaveEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (editingService != null)
            {
                using (context = new ComputerServiceManagerEntities())
                {
                    var serviceToUpdate = context.Услуги.Find(editingService.idУслуга);
                    if (serviceToUpdate != null)
                    {
                        serviceToUpdate.Наименование = txtServiceName.Text;
                        serviceToUpdate.Стоимость = decimal.Parse(txtServicePrice.Text);
                        context.SaveChanges();
                        LoadServices();
                        ClearInputs();
                    }
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (editingService != null)
            {
                // Предупреждение о текущем редактировании
                MessageBoxResult result = MessageBox.Show(
                    "Прервать редактирование и создать новую запись?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    editingService = null; // Сброс редактирования
                }
                else
                {
                    return; // Отмена добавления
                }
            }

            try
            {
                using (context = new ComputerServiceManagerEntities())
                {
                    var service = new Услуги
                    {
                        Наименование = txtServiceName.Text,
                        Стоимость = decimal.Parse(txtServicePrice.Text)
                    };
                    context.Услуги.Add(service);
                    context.SaveChanges();
                    LoadServices();
                    ClearInputs();
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("Введите корректную стоимость услуги.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            using (var context = new ComputerServiceManagerEntities())
            {
                if (ServicesDataGrid.SelectedItem is Услуги selectedService)
                {
                    try
                    {
                        var serviceToDelete = context.Услуги.Find(selectedService.idУслуга);

                        if (serviceToDelete != null)
                        {
                            try
                            {
                                MessageBoxResult result = MessageBox.Show(
                                    "Вы уверены, что хотите удалить услугу?",
                                    "Подтверждение удаления",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

                                if (result == MessageBoxResult.Yes)
                                {
                                    context.Услуги.Remove(serviceToDelete);
                                    context.SaveChanges();
                                    LoadServices();
                                    ClearInputs();
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                        }
                        else
                        {
                            MessageBox.Show("Услуга не найдена в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ClearInputs()
        {
            txtServiceName.Clear();
            txtServicePrice.Clear();
            editingService = null;
            ServicesDataGrid.SelectedIndex = -1;
        }
    }
}