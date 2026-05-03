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
using System.Windows.Shapes;

namespace ComputerServiceManager
{
    /// <summary>
    /// Логика взаимодействия для loginWindow.xaml
    /// </summary>
    public partial class loginWindow : Window
    {
        public loginWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text?.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login))
            {
                txtError.Text = "Введите логин";
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                txtError.Text = "Введите пароль";
                return;
            }

            var user = AuthService.Authenticate(login, password);

            if (user != null)
            {
                txtError.Text = "";
                // Открываем главное окно с учетом прав доступа
                var mainViewWindow = new AllTabControlWindow();
                mainViewWindow.ApplyRoleBasedAccess();
                this.Close();
                mainViewWindow.ShowDialog();
                
                // После закрытия главного окна - показываем окно входа снова
                // Это позволит пользователю войти под другой учетной записью
                this.Show();
                txtLogin.Text = "";
                txtPassword.Password = "";
                txtLogin.Focus();
            }
            else
            {
                txtError.Text = "Неверный логин или пароль";
            }
        }
    }
}
