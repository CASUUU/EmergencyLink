using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmergencyLink.Forms
{
    public sealed class RoleSelectionForm : Form
    {
        public string SelectedRole;

        public RoleSelectionForm()
        {
            Text = "选择角色 - EmergencyLink";
            Font = new Font("Microsoft YaHei UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 460;
            Height = 330;

            Label title = new Label();
            title.Text = "请选择本机角色";
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(30, 24, 384, 40);
            Controls.Add(title);

            AddRoleButton("管理者", "兜底提供服务器、管理房间和日志", RoleNames.Manager, 54, 86);
            AddRoleButton("主办方", "可兼服务器，负责正式审批和扣次", RoleNames.Organizer, 234, 86);
            AddRoleButton("选手-比赛", "接收悬浮提醒并发送回执", RoleNames.Player, 54, 178);
            AddRoleButton("选手-队友", "二次确认后发起告警", RoleNames.Teammate, 234, 178);
        }

        private void AddRoleButton(string title, string subtitle, string role, int x, int y)
        {
            Button button = new Button();
            button.Text = title + "\r\n" + subtitle;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(34, 40, 49);
            button.SetBounds(x, y, 160, 72);
            button.DialogResult = DialogResult.OK;
            button.Click += delegate
            {
                SelectedRole = role;
            };
            Controls.Add(button);
        }
    }
}
