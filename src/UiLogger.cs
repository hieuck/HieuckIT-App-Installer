using System;
using System.Drawing;
using System.Windows.Forms;

namespace HieuckIT_App_Installer
{
    public class UiLogger
    {
        private readonly RichTextBox _logTextBox;

        public UiLogger(RichTextBox logTextBox)
        {
            _logTextBox = logTextBox;
        }

        public void Log(string message, Color? color = null)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action(() => Log(message, color)));
                return;
            }

            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.SelectionLength = 0;
            _logTextBox.SelectionColor = color ?? Color.FromArgb(0, 255, 0); // Default to green
            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _logTextBox.ScrollToCaret();
        }
    }
}
