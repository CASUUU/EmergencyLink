using System;
using System.Drawing;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace EmergencyLink.Forms
{
    public sealed class OverlayForm : Form
    {
        private readonly Label _label;
        private readonly System.Windows.Forms.Timer _flashTimer;
        private bool _flash;
        private string _currentType;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 280;
            Height = 86;
            Opacity = 0.82;
            BackColor = Color.Red;

            _label = new Label();
            _label.Dock = DockStyle.Fill;
            _label.TextAlign = ContentAlignment.MiddleCenter;
            _label.ForeColor = Color.White;
            _label.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            _label.Text = "";
            Controls.Add(_label);

            _flashTimer = new System.Windows.Forms.Timer();
            _flashTimer.Interval = 360;
            _flashTimer.Tick += delegate
            {
                _flash = !_flash;
                ApplyColors();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        public void ShowAlert(string type, string target, int count, bool overLimit)
        {
            _currentType = type;
            if (type == AlertTypes.Test)
            {
                _label.Text = "测试提醒\nCtrl+Alt+R 回执";
            }
            else
            {
                _label.Text = overLimit ? "超额紧急连麦\nCtrl+Alt+R 回执" : "队友请求连麦\nCtrl+Alt+R 回执";
            }

            PositionWindow();
            _flash = false;
            ApplyColors();
            _flashTimer.Start();
            if (!Visible) Show();
            BringToFront();
            PlayAlertSound();
        }

        public void ShowAcknowledged()
        {
            _flashTimer.Stop();
            BackColor = Color.FromArgb(30, 136, 85);
            _label.Text = "已回执";
            Opacity = 0.75;
            System.Windows.Forms.Timer hideTimer = new System.Windows.Forms.Timer();
            hideTimer.Interval = 900;
            hideTimer.Tick += delegate
            {
                hideTimer.Stop();
                hideTimer.Dispose();
                Hide();
            };
            hideTimer.Start();
        }

        public void HideOverlay()
        {
            _flashTimer.Stop();
            Hide();
        }

        private void PositionWindow()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Left = area.Right - Width - 24;
            Top = area.Top + 24;
        }

        private void ApplyColors()
        {
            if (_currentType == AlertTypes.Test)
            {
                BackColor = _flash ? Color.FromArgb(20, 112, 190) : Color.FromArgb(247, 181, 56);
                _label.ForeColor = _flash ? Color.White : Color.Black;
            }
            else
            {
                BackColor = _flash ? Color.FromArgb(222, 40, 40) : Color.FromArgb(120, 0, 0);
                _label.ForeColor = Color.White;
            }
        }

        private void PlayAlertSound()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < 3; i++)
                {
                    try { SystemSounds.Exclamation.Play(); }
                    catch { }
                    Thread.Sleep(350);
                }
            });
        }
    }
}
