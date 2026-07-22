namespace FastCopyPaste.Host;

internal sealed class ConflictDialog : Form
{
    public ConflictDialog(IReadOnlyList<string> conflicts)
    {
        Text = "FastCopy 粘贴冲突";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 310);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "目标目录中已经存在同名项目。",
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18)
        };
        var explanation = new Label
        {
            Text = "继续后，FastCopy 将合并目录并按所选模式覆盖不同文件。",
            AutoSize = true,
            Location = new Point(20, 50)
        };
        var items = new TextBox
        {
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 82),
            Size = new Size(520, 160),
            Text = BuildConflictText(conflicts)
        };
        var continueButton = new Button
        {
            Text = "合并并覆盖",
            DialogResult = DialogResult.OK,
            Location = new Point(324, 262),
            Size = new Size(105, 32)
        };
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(440, 262),
            Size = new Size(100, 32)
        };

        Controls.AddRange([title, explanation, items, continueButton, cancelButton]);
        AcceptButton = cancelButton;
        CancelButton = cancelButton;
    }

    private static string BuildConflictText(IReadOnlyList<string> conflicts)
    {
        var visible = conflicts.Take(8).Select(path => "• " + path).ToList();
        if (conflicts.Count > visible.Count)
        {
            visible.Add($"……另有 {conflicts.Count - visible.Count} 项");
        }

        return string.Join(Environment.NewLine, visible);
    }
}
