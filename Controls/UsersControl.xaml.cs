using ComputerServiceManager.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ComputerServiceManager.Controls
{
    public partial class UsersControl : UserControl
    {
        private ICollectionView _userView;
        private List<object> _fullCombinedData;
        private Пользователь _currentUser;
        private bool _isEditing;

        public UsersControl()
        {
            InitializeComponent();
            LoadData();
            LoadRoles();
        }

        private void LoadRoles()
        {
            var context = ComputerServiceManagerEntities.GetContext();
            var roles = context.Роль.ToList();
            roles.Insert(0, new Роль { idРоль = -1, Наименование = "Выберите роль" });
            cmbxRole.ItemsSource = roles;

            var filterRoles = context.Роль.ToList();
            filterRoles.Insert(0, new Роль { idРоль = -1, Наименование = "Все" });
            cmbFilterRole.ItemsSource = filterRoles;
        }

        public void LoadData()
        {
            var context = ComputerServiceManagerEntities.GetContext();

            var combinedData = context.Пользователь
                .AsNoTracking()
                .Include(u => u.Роль)
                .Select(u => new
                {
                    IdПользователь = u.idПользователь,
                    Фамилия = u.Фамилия ?? "",
                    Имя = u.Имя ?? "",
                    Отчество = u.Отчество ?? "",
                    НаименованиеРоль = u.Роль.Наименование ?? "Не указана",
                    НомерТелефона = u.НомерТелефона ?? "",
                    Email = u.Email ?? "",
                    Активность = u.Активность ?? false,
                    Логин = u.Логин ?? "",
                    Пароль = u.Пароль ?? "",
                    idРоль = u.idРоль ?? -1
                })
                .ToList()
                .Select(u => new
                {
                    // Переименовано в idПользователь, чтобы binding в DataGrid работал
                    idПользователь = u.IdПользователь,
                    ФИО = $"{u.Фамилия} {u.Имя} {u.Отчество}".Trim(),
                    u.Фамилия,
                    u.Имя,
                    u.Отчество,
                    u.НаименованиеРоль,
                    u.НомерТелефона,
                    u.Email,
                    u.Активность,
                    u.Логин,
                    u.Пароль,
                    u.idРоль
                })
                .ToList();

            _fullCombinedData = combinedData.Cast<object>().ToList();
            _userView = CollectionViewSource.GetDefaultView(_fullCombinedData);
            _userView.Filter = FilterUsers;
            UsersDataGrid.ItemsSource = _userView;
        }

        private bool FilterUsers(object item)
        {
            var row = item as dynamic;
            if (row == null) return false;

            if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                var searchText = searchTextBox.Text.ToLower();
                if (!row.ФИО.ToString().ToLower().Contains(searchText) &&
                    !row.Email.ToString().ToLower().Contains(searchText) &&
                    !row.НомерТелефона.ToString().ToLower().Contains(searchText) &&
                    !row.НаименованиеРоль.ToString().ToLower().Contains(searchText) &&
                    !row.Логин.ToString().ToLower().Contains(searchText) &&
                    !row.idПользователь.ToString().Contains(searchText))
                    return false;
            }

            var selectedRole = cmbFilterRole.SelectedItem as Роль;
            if (selectedRole != null && selectedRole.idРоль != -1)
            {
                if (selectedRole.idРоль != row.idРоль)
                    return false;
            }

            return true;
        }

        private void OnFilterChanged()
        {
            _userView?.Refresh();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void cmbFilterRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void cmbFilterActive_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnFilterChanged();
        }

        private void buttonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя для редактирования");
                return;
            }

            var selectedRow = UsersDataGrid.SelectedItem as dynamic;
            int userId = (int)selectedRow.idПользователь;   // изменено с IdПользователь

            using (var context = new ComputerServiceManagerEntities())
            {
                // Загружаем БЕЗ Include, чтобы не тянуть объект Роль
                _currentUser = context.Пользователь
                    .FirstOrDefault(u => u.idПользователь == userId);

                if (_currentUser != null)
                {
                    // На всякий случай обнуляем навигационное свойство
                    _currentUser.Роль = null;

                    cmbxRole.SelectedValue = _currentUser.idРоль;
                    txtLastName.Text = _currentUser.Фамилия;
                    txtFirstName.Text = _currentUser.Имя;
                    txtMiddleName.Text = _currentUser.Отчество;
                    txtEmail.Text = _currentUser.Email;
                    txtPhone.Text = _currentUser.НомерТелефона;
                    txtLogin.Text = _currentUser.Логин;
                    txtPassword.Password = _currentUser.Пароль;
                    chkActive.IsChecked = _currentUser.Активность ?? true;

                    _isEditing = true;
                    buttonSave.IsEnabled = true;
                    mainTabControl.SelectedItem = tabEditUsers;
                }
            }
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
                _currentUser = new Пользователь();

            StringBuilder errors = new StringBuilder();

            if (cmbxRole.SelectedItem == null || (int)cmbxRole.SelectedValue == -1)
                errors.AppendLine("- Выберите роль.");
            if (string.IsNullOrWhiteSpace(txtLastName.Text))
                errors.AppendLine("- Укажите фамилию.");
            if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                errors.AppendLine("- Укажите имя.");
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
                errors.AppendLine("- Укажите логин.");
            if (string.IsNullOrWhiteSpace(txtPassword.Password) && _currentUser.idПользователь == 0)
                errors.AppendLine("- Укажите пароль.");
            if (string.IsNullOrWhiteSpace(txtEmail.Text))
                errors.AppendLine("- Укажите email.");

            if (errors.Length > 0)
            {
                MessageBox.Show($"Исправьте ошибки:\n{errors}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _currentUser.idРоль = (int)cmbxRole.SelectedValue;
                _currentUser.Фамилия = txtLastName.Text;
                _currentUser.Имя = txtFirstName.Text;
                _currentUser.Отчество = txtMiddleName.Text;
                _currentUser.Email = txtEmail.Text;
                _currentUser.НомерТелефона = txtPhone.Text;
                _currentUser.Логин = txtLogin.Text;
                if (!string.IsNullOrWhiteSpace(txtPassword.Password))
                    _currentUser.Пароль = txtPassword.Password;
                _currentUser.Активность = chkActive.IsChecked ?? true;

                // Гарантируем, что навигационное свойство не помешает сохранению
                _currentUser.Роль = null;

                using (var context = new ComputerServiceManagerEntities())
                {
                    if (_currentUser.idПользователь == 0)
                    {
                        // Для новой записи не добавляем объект Роль
                        _currentUser.Роль = null;
                        context.Пользователь.Add(_currentUser);
                    }
                    else
                    {
                        // Присоединяем как изменённый, предварительно очистив свойство Роль
                        context.Entry(_currentUser).State = EntityState.Modified;
                    }

                    context.SaveChanges();
                }

                LoadData();
                ClearForm();
                MessageBox.Show("Пользователь сохранен успешно", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя для удаления");
                return;
            }

            var selectedRow = UsersDataGrid.SelectedItem as dynamic;
            int userId = (int)selectedRow.idПользователь;   // изменено

            if (MessageBox.Show("Удалить пользователя?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var context = new ComputerServiceManagerEntities())
                {
                    var user = context.Пользователь
                        .FirstOrDefault(u => u.idПользователь == userId);

                    if (user != null)
                    {
                        context.Пользователь.Remove(user);
                        context.SaveChanges();

                        LoadData();
                        if (_currentUser?.idПользователь == userId)
                            ClearForm();
                    }
                }
            }
        }

        private void ClearForm()
        {
            _currentUser = null;
            _isEditing = false;
            mainTabControl.SelectedItem = tabDataGridForUsers;

            cmbxRole.SelectedValue = 0;
            txtLastName.Text = "";
            txtFirstName.Text = "";
            txtMiddleName.Text = "";
            txtEmail.Text = "";
            txtPhone.Text = "";
            txtLogin.Text = "";
            txtPassword.Password = "";
            chkActive.IsChecked = false;
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isEditing && mainTabControl.SelectedItem == tabEditUsers)
            {
                if (MessageBox.Show("Закрыть форму редактирования?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    UsersDataGrid.SelectedItem = null;
                }
            }
        }

        private void buttonClean_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }
    }
}