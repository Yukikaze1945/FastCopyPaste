namespace FastCopyPaste.Host;

internal sealed class ConflictDialog : Form
{
    private readonly Button _cancelButton;

    public ConflictDialog(IReadOnlyList<string> conflicts)
    {
        Text = "FastCopy 粘贴冲突";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(720, 420);
        MinimumSize = new Size(680, 400);
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Microsoft YaHei UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 22, 18),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "目标目录中已经存在同名项目。",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };
        var explanation = new Label
        {
            Text = "继续后，FastCopy 将合并目录并按所选模式覆盖不同文件。",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12)
        };
        var items = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            MinimumSize = new Size(0, 200),
            Margin = new Padding(0, 0, 0, 14)
        };
        items.Items.AddRange(BuildConflictItems(conflicts).Cast<object>().ToArray());

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        _cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(112, 38),
            Margin = Padding.Empty
        };
        var continueButton = new Button
        {
            Text = "合并并覆盖",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(132, 38),
            Margin = new Padding(0, 0, 10, 0)
        };

        buttons.Controls.AddRange([_cancelButton, continueButton]);
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(explanation, 0, 1);
        layout.Controls.Add(items, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        layout.SizeChanged += (_, _) => explanation.MaximumSize =
            new Size(Math.Max(200, layout.ClientSize.Width - layout.Padding.Horizontal), 0);

        AcceptButton = _cancelButton;
        CancelButton = _cancelButton;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _cancelButton.Focus();
        Activate();
        NativeMethods.SetForegroundWindow(Handle);
    }

    internal static IReadOnlyList<string> BuildConflictItems(IReadOnlyList<string> conflicts)
    {
        var visible = conflicts.Take(8).Select(path => "• " + path).ToList();
        if (conflicts.Count > visible.Count)
        {
            visible.Add($"……另有 {conflicts.Count - visible.Count} 项");
        }

        return visible;
    }
}

internal sealed class WindowHandleOwner(nint handle) : IWin32Window
{
    public nint Handle { get; } = handle;
}
