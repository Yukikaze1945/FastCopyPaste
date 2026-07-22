using System.ComponentModel;

namespace FastCopyPaste.Host;

internal sealed class HotkeyDialog : Form
{
    private readonly KeyboardHook _keyboardHook;
    private readonly Label _gestureLabel;
    private readonly Label _statusLabel;
    private readonly Button _recordButton;
    private readonly Button _saveButton;
    private bool _enabledTemporarily;

    public HotkeyDialog(KeyboardHook keyboardHook, HotkeyGesture currentGesture)
    {
        _keyboardHook = keyboardHook;
        SelectedGesture = currentGesture.Normalize();

        Text = "设置 FastCopy 粘贴快捷键";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(620, 350);
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Microsoft YaHei UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 20),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "FastCopy 粘贴快捷键",
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        _gestureLabel = new Label
        {
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            BackColor = Color.FromArgb(239, 246, 252),
            ForeColor = Color.FromArgb(18, 59, 104),
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(12),
            MinimumSize = new Size(0, 92)
        };
        _statusLabel = new Label
        {
            Text = "允许任意非修饰键以及任意修饰键组合。",
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 12)
        };

        var actionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 18)
        };
        _recordButton = new Button
        {
            Text = "录制新快捷键",
            AutoSize = true,
            MinimumSize = new Size(150, 40),
            Margin = Padding.Empty
        };
        _recordButton.Click += StartRecording;
        var restoreButton = new Button
        {
            Text = "恢复 Ctrl+V",
            AutoSize = true,
            MinimumSize = new Size(140, 40),
            Margin = new Padding(10, 0, 0, 0)
        };
        restoreButton.Click += (_, _) =>
        {
            StopRecording();
            SelectedGesture = HotkeyGesture.Default;
            UpdateGestureDisplay();
            _statusLabel.Text = "已选择默认快捷键；点击保存后生效。";
        };
        actionButtons.Controls.AddRange([_recordButton, restoreButton]);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(110, 38),
            Margin = Padding.Empty
        };
        _saveButton = new Button
        {
            Text = "保存",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(110, 38),
            Margin = new Padding(0, 0, 10, 0)
        };
        footer.Controls.AddRange([cancelButton, _saveButton]);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_gestureLabel, 0, 1);
        layout.Controls.Add(_statusLabel, 0, 2);
        layout.Controls.Add(actionButtons, 0, 3);
        layout.Controls.Add(footer, 0, 4);
        Controls.Add(layout);

        AcceptButton = _saveButton;
        CancelButton = cancelButton;
        UpdateGestureDisplay();
    }

    public HotkeyGesture SelectedGesture { get; private set; }

    private void StartRecording(object? sender, EventArgs args)
    {
        try
        {
            if (!_keyboardHook.Enabled)
            {
                _keyboardHook.Enable();
                _enabledTemporarily = true;
            }

            _recordButton.Enabled = false;
            _saveButton.Enabled = false;
            _recordButton.Text = "正在录制…";
            _gestureLabel.Text = "请按下快捷键";
            _statusLabel.Text = "可使用单键或任意 Ctrl / Alt / Shift / Win 组合。";
            _keyboardHook.BeginCapture(CompleteRecording);
        }
        catch (Win32Exception exception)
        {
            StopRecording();
            MessageBox.Show(
                this,
                exception.Message,
                "无法录制快捷键",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CompleteRecording(HotkeyGesture gesture)
    {
        SelectedGesture = gesture.Normalize();
        _recordButton.Enabled = true;
        _saveButton.Enabled = true;
        _recordButton.Text = "录制新快捷键";
        UpdateGestureDisplay();
        _statusLabel.Text = "已录制；点击保存后立即生效。";
    }

    private void StopRecording()
    {
        _keyboardHook.CancelCapture();
        _recordButton.Enabled = true;
        _saveButton.Enabled = true;
        _recordButton.Text = "录制新快捷键";
        DisableTemporaryHook();
    }

    private void DisableTemporaryHook()
    {
        if (!_enabledTemporarily)
        {
            return;
        }

        _keyboardHook.Disable();
        _enabledTemporarily = false;
    }

    private void UpdateGestureDisplay() =>
        _gestureLabel.Text = SelectedGesture.ToDisplayString();

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopRecording();
        base.OnFormClosed(e);
    }
}
