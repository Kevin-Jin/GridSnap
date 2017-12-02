using System;
using System.Drawing;
using System.Windows.Forms;

namespace GridSnap
{
    internal partial class View : Form
    {
        private const int WM_MOVE = 0x0003, WM_SIZE = 0x0005, WM_WINDOWPOSCHANGING = 0x0046, WM_WINDOWPOSCHANGED = 0x0047, WM_SIZING = 0x0214, WM_MOVING = 0x0216;

        private readonly Model model;

        internal View(Model m)
        {
            model = m;
            InitializeComponent();
            
            ResetRows();
            ResetCols();
        }

        private void ResetRows()
        {
            txtRows.Text = model.GetScreenSettings(Screen.FromControl(this)).GridRows.ToString();
        }

        private void ResetCols()
        {
            txtCols.Text = model.GetScreenSettings(Screen.FromControl(this)).GridCols.ToString();
        }

        private void View_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            showToolStripMenuItem.PerformClick();
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtRows_Enter(object sender, EventArgs e)
        {
            ResetRows();
        }

        private void txtRows_Leave(object sender, EventArgs e)
        {
            if (int.TryParse(txtRows.Text, out int rows))
            {
                model.GetScreenSettings(Screen.FromControl(this)).GridRows = rows;
                txtRows.BackColor = SystemColors.Window;
            }
            else
            {
                txtRows.BackColor = Color.Red;
            }
        }

        private void txtCols_Enter(object sender, EventArgs e)
        {
            ResetCols();
        }

        private void txtCols_Leave(object sender, EventArgs e)
        {
            if (int.TryParse(txtCols.Text, out int cols))
            {
                model.GetScreenSettings(Screen.FromControl(this)).GridCols = cols;
                txtCols.BackColor = SystemColors.Window;
            }
            else
            {
                txtCols.BackColor = Color.Red;
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOVING:
                    break;
                case WM_MOVE:
                    break;
                case WM_SIZING:
                    break;
                case WM_SIZE:
                    break;
                case WM_WINDOWPOSCHANGING:
                    break;
                case WM_WINDOWPOSCHANGED:
                    ResetRows();
                    ResetCols();
                    break;
            }
            base.WndProc(ref m);
        }
    }
}
