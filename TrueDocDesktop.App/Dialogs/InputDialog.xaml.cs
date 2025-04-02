using System.Windows;

namespace TrueDocDesktop.App.Dialogs
{
    /// <summary>
    /// Simple dialog to get a single text input from the user
    /// </summary>
    public partial class InputDialog : Window
    {
        public string Answer { get; private set; }

        public InputDialog(string question, string title, string defaultAnswer = "")
        {
            InitializeComponent();
            Title = title;
            lblQuestion.Text = question;
            txtAnswer.Text = defaultAnswer;
            txtAnswer.SelectAll();
            txtAnswer.Focus();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            Answer = txtAnswer.Text;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 