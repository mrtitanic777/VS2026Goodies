using System.Windows.Forms;

namespace Xbox360Package
{
    /// <summary>A minimal single-line text input dialog.</summary>
    internal static class ConsolePrompt
    {
        public static string Show(string title, string prompt, string defaultValue = "")
        {
            using (var form = new Form
            {
                Text = title,
                Width = 430,
                Height = 165,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var label = new Label { Left = 12, Top = 14, Width = 400, Text = prompt };
                var textBox = new TextBox { Left = 12, Top = 42, Width = 400, Text = defaultValue };
                var ok = new Button { Text = "OK", Left = 256, Width = 75, Top = 80, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 337, Width = 75, Top = 80, DialogResult = DialogResult.Cancel };
                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }
    }
}
