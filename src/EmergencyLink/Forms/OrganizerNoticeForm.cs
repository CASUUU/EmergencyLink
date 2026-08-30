using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace EmergencyLink.Forms
{
    public sealed class OrganizerNoticeForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _detailLabel;
        private readonly Button _viewButton;
        private readonly Button _dismissButton;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private bool _pulse;
        private bool _dragging;
        private Point _dragStart;
        private string _currentBatchId = "";
        private int _lastCount;
        private bool _dismissedCurrentNotice;
        private Color _colorA;
        private Color _colorB;

        public event Action ViewRequested;

        public OrganizerNoticeForm()
        {
            Text = "EmergencyLink 提醒";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 360;
            Height = 170;
            MinimumSize = new Size(320, 150);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(126, 22, 42);

            _titleLabel = new Label();
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.ForeColor = Color.White;
            _titleLabel.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_titleLabel);

            _detailLabel = new Label();
            _detailLabel.BackColor = Color.Transparent;
            _detailLabel.ForeColor = Color.FromArgb(250, 250, 250);
            _detailLabel.Font = new Font("Microsoft YaHei UI", 9F);
            _detailLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_detailLabel);

            _viewButton = new Button();
            _viewButton.Text = "查看审批";
            _viewButton.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _viewButton.FlatStyle = FlatStyle.Flat;
            _viewButton.FlatAppearance.BorderSize = 0;
            _viewButton.BackColor = Color.White;
            _viewButton.ForeColor = Color.FromArgb(126, 22, 42);
            _viewButton.Click += delegate
            {
                Action handler = ViewRequested;
                if (handler != null) handler();
            };
            Controls.Add(_viewButton);

            _dismissButton = new Button();
            _dismissButton.Text = "已知晓";
            _dismissButton.Font = new Font("Microsoft YaHei UI", 10F);
            _dismissButton.FlatStyle = FlatStyle.Flat;
            _dismissButton.FlatAppearance.BorderSize = 0;
            _dismissButton.BackColor = Color.FromArgb(255, 232, 232);
            _dismissButton.ForeColor = Color.FromArgb(126, 22, 42);
            _dismissButton.Click += delegate { DismissNotice(); };
            Controls.Add(_dismissButton);

            MouseDown += StartDrag;
            MouseMove += DragMove;
            MouseUp += EndDrag;
            _titleLabel.MouseDown += StartDrag;
            _titleLabel.MouseMove += DragMove;
            _titleLabel.MouseUp += EndDrag;
            _detailLabel.MouseDown += StartDrag;
            _detailLabel.MouseMove += DragMove;
            _detailLabel.MouseUp += EndDrag;

            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 520;
            _pulseTimer.Tick += delegate
            {
                _pulse = !_pulse;
                ApplyColors();
            };

            LayoutControls();
            PositionWindow();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_titleLabel != null) LayoutControls();
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 16))
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
            using (GraphicsPath path = RoundedRect(rect, 16))
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, _colorA, _colorB, 30F))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (Pen pen = new Pen(Color.FromArgb(125, Color.White), 1))
            using (GraphicsPath path = RoundedRect(rect, 16))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        public void ShowNotice(AlertView batch, int remainingCalls)
        {
            if (batch == null) return;
            bool isNewOrRepeated = _currentBatchId != batch.Id || _lastCount != batch.Count;
            if (isNewOrRepeated) _dismissedCurrentNotice = false;
            _currentBatchId = batch.Id;
            _lastCount = batch.Count;

            _titleLabel.Text = batch.IsOverLimit ? "超额紧急连麦请求" : "收到正式连麦请求";
            string detail = "目标选手：" + batch.Target + "    剩余次数：" + remainingCalls.ToString();
            if (!String.IsNullOrEmpty(batch.Initiators)) detail += "\r\n发起人：" + batch.Initiators;
            if (batch.Count > 1) detail += "    同批提醒：" + batch.Count.ToString() + " 次";
            _detailLabel.Text = detail;

            if (_dismissedCurrentNotice) return;

            _pulse = false;
            ApplyColors();
            _pulseTimer.Start();
            PositionWindow();
            if (!Visible) Show();
            BringToFront();

            if (isNewOrRepeated) PlayNoticeSound();
        }

        public void ClearNotice()
        {
            _currentBatchId = "";
            _lastCount = 0;
            _dismissedCurrentNotice = false;
            _pulseTimer.Stop();
            Hide();
        }

        private void DismissNotice()
        {
            _dismissedCurrentNotice = true;
            _pulseTimer.Stop();
            Hide();
        }

        private void LayoutControls()
        {
            int margin = 16;
            _titleLabel.SetBounds(margin, 14, Width - margin * 2, 32);
            _detailLabel.SetBounds(margin, 48, Width - margin * 2, 52);
            _viewButton.SetBounds(58, Height - 52, 112, 34);
            _dismissButton.SetBounds(190, Height - 52, 112, 34);
        }

        private void PositionWindow()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Left = area.Right - Width - 28;
            Top = area.Top + 190;
        }

        private void ApplyColors()
        {
            _colorA = _pulse ? Color.FromArgb(226, 70, 82) : Color.FromArgb(126, 22, 42);
            _colorB = _pulse ? Color.FromArgb(238, 126, 82) : Color.FromArgb(88, 18, 58);
            Invalidate();
        }

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStart = e.Location;
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point screenPoint = PointToScreen(e.Location);
            Location = new Point(screenPoint.X - _dragStart.X, screenPoint.Y - _dragStart.Y);
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

        private void PlayNoticeSound()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Console.Beep(880, 120);
                    Thread.Sleep(70);
                    Console.Beep(1046, 140);
                    Thread.Sleep(70);
                    Console.Beep(988, 160);
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
