using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace EmergencyLink.Forms
{
    public sealed class OverlayForm : Form
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Label _quotaLabel;
        private readonly Button _ackButton;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private bool _pulse;
        private bool _hasManualPosition;
        private bool _dragging;
        private bool _alertActive;
        private Point _dragStart;
        private string _currentType;
        private string _statusPhase;
        private int _remainingCalls;
        private Color _colorA;
        private Color _colorB;

        public event Action AcknowledgementRequested;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 300;
            Height = 150;
            MinimumSize = new Size(240, 126);
            Opacity = 0.96;
            BackColor = Color.FromArgb(36, 38, 45);
            DoubleBuffered = true;

            _titleLabel = new Label();
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.ForeColor = Color.White;
            _titleLabel.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.BackColor = Color.Transparent;
            _subtitleLabel.ForeColor = Color.FromArgb(245, 246, 248);
            _subtitleLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            _subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_subtitleLabel);

            _quotaLabel = new Label();
            _quotaLabel.BackColor = Color.Transparent;
            _quotaLabel.ForeColor = Color.FromArgb(245, 246, 248);
            _quotaLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            _quotaLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_quotaLabel);

            _ackButton = new Button();
            _ackButton.Text = "等待提醒";
            _ackButton.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _ackButton.FlatStyle = FlatStyle.Flat;
            _ackButton.FlatAppearance.BorderSize = 0;
            _ackButton.BackColor = Color.White;
            _ackButton.ForeColor = Color.FromArgb(36, 38, 45);
            _ackButton.Enabled = false;
            _ackButton.Click += delegate
            {
                Action handler = AcknowledgementRequested;
                if (handler != null) handler();
            };
            Controls.Add(_ackButton);

            MouseDown += StartDrag;
            MouseMove += DragMove;
            MouseUp += EndDrag;
            _titleLabel.MouseDown += StartDrag;
            _titleLabel.MouseMove += DragMove;
            _titleLabel.MouseUp += EndDrag;
            _subtitleLabel.MouseDown += StartDrag;
            _subtitleLabel.MouseMove += DragMove;
            _subtitleLabel.MouseUp += EndDrag;
            _quotaLabel.MouseDown += StartDrag;
            _quotaLabel.MouseMove += DragMove;
            _quotaLabel.MouseUp += EndDrag;

            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 420;
            _pulseTimer.Tick += delegate
            {
                _pulse = !_pulse;
                ApplyAlertColors();
            };

            _statusPhase = PhaseNames.Preparation;
            _remainingCalls = 3;
            SetStatus(_statusPhase, _remainingCalls);
            PositionWindow();
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
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WmNcHitTest || (int)m.Result != HtClient) return;

            Point cursor = PointToClient(Cursor.Position);
            int grip = 8;
            bool left = cursor.X <= grip;
            bool right = cursor.X >= Width - grip;
            bool top = cursor.Y <= grip;
            bool bottom = cursor.Y >= Height - grip;

            if (left && top) m.Result = (IntPtr)HtTopLeft;
            else if (right && top) m.Result = (IntPtr)HtTopRight;
            else if (left && bottom) m.Result = (IntPtr)HtBottomLeft;
            else if (right && bottom) m.Result = (IntPtr)HtBottomRight;
            else if (left) m.Result = (IntPtr)HtLeft;
            else if (right) m.Result = (IntPtr)HtRight;
            else if (top) m.Result = (IntPtr)HtTop;
            else if (bottom) m.Result = (IntPtr)HtBottom;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_titleLabel != null) LayoutControls();
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 18))
            {
                Region = new Region(path);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, 18))
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, _colorA, _colorB, 35F))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (Pen pen = new Pen(Color.FromArgb(120, Color.White), 1))
            using (GraphicsPath path = RoundedRect(rect, 18))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        public void SetStatus(string phase, int remainingCalls)
        {
            _statusPhase = String.IsNullOrEmpty(phase) ? PhaseNames.Preparation : phase;
            _remainingCalls = remainingCalls < 0 ? 0 : remainingCalls;

            if (_alertActive) return;
            ApplyStatusView();
        }

        public void ShowStatus(string phase, int remainingCalls)
        {
            SetStatus(phase, remainingCalls);
            if (!_hasManualPosition) PositionWindow();
            if (!Visible) Show();
            BringToFront();
        }

        public void ShowAlert(string type, string target, int count, bool overLimit, string phase, int remainingCalls)
        {
            _statusPhase = String.IsNullOrEmpty(phase) ? _statusPhase : phase;
            _remainingCalls = remainingCalls < 0 ? 0 : remainingCalls;
            _currentType = type;
            _alertActive = true;

            if (type == AlertTypes.Test)
            {
                _titleLabel.Text = "测试提醒";
                _subtitleLabel.Text = "赛前通讯测试";
            }
            else
            {
                _titleLabel.Text = overLimit ? "超额紧急连麦" : "队友请求连麦";
                _subtitleLabel.Text = count > 1 ? "同批提醒 " + count.ToString() + " 次" : "请立即确认";
            }

            _quotaLabel.Text = "剩余连麦次数：" + _remainingCalls.ToString();
            _ackButton.Enabled = true;
            _ackButton.Text = "已收到";
            _pulse = false;
            ApplyAlertColors();
            _pulseTimer.Start();
            if (!_hasManualPosition) PositionWindow();
            if (!Visible) Show();
            BringToFront();
            PlayAlertSound(type);
        }

        public void ShowAcknowledged()
        {
            _pulseTimer.Stop();
            _alertActive = false;
            _titleLabel.Text = "已回执";
            _subtitleLabel.Text = "提醒状态已同步";
            _quotaLabel.Text = "剩余连麦次数：" + _remainingCalls.ToString();
            _ackButton.Enabled = false;
            _ackButton.Text = "已发送";
            _colorA = Color.FromArgb(24, 137, 92);
            _colorB = Color.FromArgb(13, 94, 122);
            Invalidate();

            System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1100;
            statusTimer.Tick += delegate
            {
                statusTimer.Stop();
                statusTimer.Dispose();
                ApplyStatusView();
            };
            statusTimer.Start();
        }

        public void HideOverlay()
        {
            _pulseTimer.Stop();
            _alertActive = false;
            Hide();
        }

        private void LayoutControls()
        {
            int margin = 14;
            int contentWidth = Math.Max(120, Width - margin * 2);
            int buttonWidth = Math.Max(130, Math.Min(220, Width - 72));
            int buttonHeight = Math.Max(32, Math.Min(42, Height / 4));
            int buttonTop = Height - buttonHeight - 14;

            _titleLabel.SetBounds(margin, 12, contentWidth, 30);
            _subtitleLabel.SetBounds(margin, 42, contentWidth, 22);
            _quotaLabel.SetBounds(margin, 64, contentWidth, 22);
            _ackButton.SetBounds((Width - buttonWidth) / 2, buttonTop, buttonWidth, buttonHeight);
        }

        private void ApplyStatusView()
        {
            _pulseTimer.Stop();
            _alertActive = false;
            _currentType = "";
            _titleLabel.Text = GetPhaseTitle(_statusPhase);
            _subtitleLabel.Text = GetPhaseSubtitle(_statusPhase);
            _quotaLabel.Text = "剩余连麦次数：" + _remainingCalls.ToString();
            _ackButton.Enabled = false;
            _ackButton.Text = "等待提醒";
            ApplyStatusColors(_statusPhase);
            Invalidate();
        }

        private string GetPhaseTitle(string phase)
        {
            if (phase == PhaseNames.PreMatchTest) return "比赛测试";
            if (phase == PhaseNames.InMatch) return "比赛中";
            if (phase == PhaseNames.Ended) return "比赛已结束";
            return "准备中";
        }

        private string GetPhaseSubtitle(string phase)
        {
            if (phase == PhaseNames.PreMatchTest) return "可进行通讯测试";
            if (phase == PhaseNames.InMatch) return "正式告警待命";
            if (phase == PhaseNames.Ended) return "不再接收新提醒";
            return "等待比赛开始";
        }

        private void ApplyStatusColors(string phase)
        {
            if (phase == PhaseNames.PreMatchTest)
            {
                _colorA = Color.FromArgb(28, 116, 170);
                _colorB = Color.FromArgb(35, 154, 168);
                _ackButton.ForeColor = Color.FromArgb(28, 116, 170);
            }
            else if (phase == PhaseNames.InMatch)
            {
                _colorA = Color.FromArgb(28, 121, 86);
                _colorB = Color.FromArgb(22, 100, 124);
                _ackButton.ForeColor = Color.FromArgb(28, 121, 86);
            }
            else if (phase == PhaseNames.Ended)
            {
                _colorA = Color.FromArgb(70, 74, 82);
                _colorB = Color.FromArgb(45, 48, 56);
                _ackButton.ForeColor = Color.FromArgb(70, 74, 82);
            }
            else
            {
                _colorA = Color.FromArgb(73, 82, 96);
                _colorB = Color.FromArgb(52, 61, 76);
                _ackButton.ForeColor = Color.FromArgb(73, 82, 96);
            }
            _ackButton.BackColor = Color.White;
        }

        private void PositionWindow()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Left = area.Right - Width - 26;
            Top = area.Top + 26;
        }

        private void ApplyAlertColors()
        {
            if (_currentType == AlertTypes.Test)
            {
                _colorA = _pulse ? Color.FromArgb(24, 132, 166) : Color.FromArgb(247, 172, 48);
                _colorB = _pulse ? Color.FromArgb(18, 96, 144) : Color.FromArgb(230, 118, 42);
                _ackButton.ForeColor = Color.FromArgb(25, 96, 140);
            }
            else
            {
                _colorA = _pulse ? Color.FromArgb(225, 68, 82) : Color.FromArgb(126, 22, 42);
                _colorB = _pulse ? Color.FromArgb(245, 111, 86) : Color.FromArgb(89, 14, 42);
                _ackButton.ForeColor = Color.FromArgb(126, 22, 42);
            }
            _ackButton.BackColor = Color.White;
            Invalidate();
        }

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Point cursor = PointToClient(Cursor.Position);
            int grip = 8;
            if (cursor.X <= grip || cursor.X >= Width - grip || cursor.Y <= grip || cursor.Y >= Height - grip) return;
            _dragging = true;
            _dragStart = e.Location;
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point screenPoint = PointToScreen(e.Location);
            Location = new Point(screenPoint.X - _dragStart.X, screenPoint.Y - _dragStart.Y);
            _hasManualPosition = true;
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void PlayAlertSound(string type)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (type == AlertTypes.Test)
                    {
                        Console.Beep(659, 120);
                        Thread.Sleep(70);
                        Console.Beep(784, 140);
                    }
                    else
                    {
                        Console.Beep(784, 110);
                        Thread.Sleep(50);
                        Console.Beep(988, 130);
                        Thread.Sleep(60);
                        Console.Beep(880, 150);
                    }
                }
                catch
                {
                    try { SystemSounds.Asterisk.Play(); }
                    catch { }
                }
            });
        }
    }
}
