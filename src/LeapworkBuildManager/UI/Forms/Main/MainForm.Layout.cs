using System;
using System.Drawing;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        static Label Caption(string value)
        {
            return new Label
            {
                Text = value,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
                ForeColor = Theme.Caption
            };
        }

        int animationReservedHeight, animationHistoryTop;
        public int FullLayoutPasses { get; private set; }
        public int AnimationFrames { get; private set; }

        void SetDownloadPanel(bool expanded)
        {
            int target = expanded ? 228 : 0;
            panelExpanded = expanded;
            if (expandTimer.Enabled && expansionTarget == target && appState != ApplicationState.Completed)
                return;
            if (panelHeight == target)
            {
                expandTimer.Stop();
                expansionTarget = target;
                return;
            }

            expandTimer.Stop();
            expansionStart = panelHeight;
            expansionTarget = target;
            if (!IsHandleCreated || appState == ApplicationState.Completed || !preferencesLoaded)
            {
                panelHeight = target;
                ReflowDownloadPanel();
                return;
            }

            // Reserve the larger height so the window and scrollbars stay stable.
            animationReservedHeight = Math.Max(expansionStart, expansionTarget);
            expansionClock.Restart();
            expandTimer.Start();
            ReflowDownloadPanel();
        }

        void AdvancePanelAnimation()
        {
            double fraction = Math.Min(1, expansionClock.Elapsed.TotalMilliseconds / OperationalSettings.AnimationDurationMilliseconds);
            double eased = 1 - Math.Pow(1 - fraction, 3);
            int next = expansionStart + (int)Math.Round((expansionTarget - expansionStart) * eased);
            if (next != panelHeight)
            {
                panelHeight = next;
                AnimationFrames++;
                ApplyPanelFrame();
            }

            if (fraction >= 1)
            {
                expandTimer.Stop();
                panelHeight = expansionTarget;
                ReflowDownloadPanel();
                if (panelExpanded && progressCard.Visible)
                {
                    var visible = new Rectangle(-viewport.AutoScrollPosition.X, -viewport.AutoScrollPosition.Y, viewport.ClientSize.Width, viewport.ClientSize.Height);
                    if (!visible.Contains(progressCard.Bounds))
                        viewport.ScrollControlIntoView(progressCard);
                }
            }
        }

        void ApplyPanelFrame()
        {
            canvas.SuspendLayout();
            try
            {
                progressCard.Visible = panelHeight > 0;
                progressCard.Height = Px(panelHeight);
                if (historyCard.Visible)
                    historyCard.Top = animationHistoryTop - Px(animationReservedHeight - panelHeight);
            }
            finally
            {
                canvas.ResumeLayout(false);
            }
        }

        void ReflowDownloadPanel()
        {
            if (!layoutReady || applyingLayout)
                return;
            FullLayoutPasses++;
            var area = WorkingAreaProvider();
            var ui = UiModel;
            bool completedLayout = ui.Completed || appState == ApplicationState.Preparing && controller.CompletedDownload != null;
            bool narrow = Math.Min(Px(620), area.Width) < Px(560);
            int detailHeight = narrow ? (advancedMode ? 256 : 192) : (advancedMode ? 184 : 128);
            int rowHeight = narrow ? 52 : 36;
            int resultHeight = matches.Items.Count > 0 ? 44 + matches.Items.Count * rowHeight : 72;
            var sections = new Control[]
            {
                details,
                resultsCard,
                download,
                viewLink,
                linkCard,
                idleStatus,
                progressCard,
                historyCard
            };
            var heights = new int[]
            {
                detailHeight,
                resultHeight,
                42,
                32,
                112,
                28,
                expandTimer.Enabled ? animationReservedHeight : panelHeight,
                82
            };
            var visible = new bool[]
            {
                true,
                ui.ShowResults,
                ui.ShowDownload,
                ui.ShowLinkToggle,
                ui.ShowLink,
                !panelExpanded && appState != ApplicationState.Idle && appState != ApplicationState.Loading,
                expandTimer.Enabled || panelHeight > 0,
                ui.ShowHistory
            };
            int logicalHeight = 94;
            for (int i = 0; i < heights.Length; i++)
                if (visible[i])
                    logicalHeight += heights[i] + 12;
            var plan = WindowLayoutPlan.Calculate(area, layoutScale, logicalHeight, SystemInformation.VerticalScrollBarWidth);
            int scrollY = -viewport.AutoScrollPosition.Y;
            applyingLayout = true;
            SuspendLayout();
            viewport.SuspendLayout();
            canvas.SuspendLayout();
            try
            {
                viewport.AutoScroll = false;
                viewport.AutoScrollMinSize = Size.Empty;
                viewport.AutoScrollPosition = Point.Empty;
                MinimumSize = Size.Empty;
                ClientSize = plan.WindowSize;
                viewport.SetBounds(0, Px(34), ClientSize.Width, Math.Max(1, ClientSize.Height - Px(34)));
                canvas.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                canvas.SetBounds(0, 0, plan.CanvasWidth, plan.ContentHeight);
                int y = 90;
                int width = Math.Max(Px(100), plan.CanvasWidth - Px(48));
                for (int i = 0; i < sections.Length; i++)
                {
                    sections[i].Visible = visible[i];
                    sections[i].SetBounds(Px(24), Px(y), width, Px(heights[i]));
                    if (visible[i])
                        y += heights[i] + 12;
                }

                if (expandTimer.Enabled)
                    animationHistoryTop = historyCard.Top;
                int inner = width - Px(32);
                build.SetBounds(Px(16), Px(38), narrow ? inner : (inner - Px(12)) * 56 / 100, Px(27));
                check.SetBounds(narrow ? Px(16) : build.Right + Px(12), Px(narrow ? 74 : 32), narrow ? inner : inner - build.Width - Px(12), Px(39));
                validation.SetBounds(Px(16), Px(narrow ? 116 : 71), inner, Px(23));
                advanced.SetBounds(Px(16), Px(narrow ? 144 : 96), Math.Max(Px(80), inner - Px(114)), Px(28));
                reset.SetBounds(width - Px(118), advanced.Top, Px(102), Px(28));
                if (type != null)
                {
                    typeLabel.SetBounds(Px(16), Px(narrow ? 184 : 140), Px(90), Px(24));
                    type.SetBounds(Px(narrow ? 16 : 110), Px(narrow ? 210 : 136), narrow ? inner : width - Px(126), Px(28));
                }

                resultSummary.Height = Px(matches.Items.Count > 0 ? 24 : 52);
                matches.Visible = matches.Items.Count > 0;
                matches.ItemHeight = Px(rowHeight);
                matches.Top = Px(38);
                matches.Height = Px(matches.Items.Count * rowHeight);
                downloadIdentity.SetBounds(Px(16), Px(14), Math.Max(Px(50), inner - Px(148)), Px(36));
                downloadIdentity.TextAlign = ContentAlignment.MiddleLeft;
                folder.Location = cancel.Location = new Point(width - Px(148), Px(14));
                percentage.SetBounds(Px(16), Px(58), inner, Px(24));
                bar.Top = Px(92);
                status.Top = Px(110);
                eta.Top = Px(140);
                destinationLabel.SetBounds(Px(16), Px(completedLayout ? 146 : 170), Math.Max(Px(40), inner - Px(148)), Px(44));
                copyFolderPath.SetBounds(width - Px(156), destinationLabel.Top, Px(140), Px(32));
                open.Left = Px(ui.ShowCopy ? 134 : 14);
                recent.SetBounds(Px(16), Px(38), Math.Max(Px(40), inner - Px(140)), Px(28));
                clearHistory.SetBounds(width - Px(144), Px(34), Px(128), Px(36));
                titleBar.SetBounds(0, 0, ClientSize.Width, Px(34));
                close.SetBounds(titleBar.Width - Px(44), Px(2), Px(40), Px(30));
                minimize.SetBounds(close.Left - Px(44), Px(2), Px(40), Px(30));
                help.SetBounds(minimize.Left - Px(124), Px(2), Px(116), Px(30));
                titleCaption.SetBounds(Px(16), Px(8), Math.Max(0, help.Left - Px(24)), Px(22));
                viewport.AutoScroll = true;
                viewport.AutoScrollMinSize = new Size(0, plan.ContentHeight);
            }
            finally
            {
                canvas.ResumeLayout(false);
                viewport.ResumeLayout(true);
                ResumeLayout(true);
                applyingLayout = false;
            }

            if (expandTimer.Enabled)
                ApplyPanelFrame();
            canvas.Width = Math.Min(plan.CanvasWidth, viewport.ClientSize.Width);
            viewport.AutoScrollPosition = new Point(0, plan.VerticalScroll ? Math.Min(scrollY, Math.Max(0, plan.ContentHeight - viewport.ClientSize.Height)) : 0);
            if (Visible && WindowState == FormWindowState.Normal)
            {
                Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
                Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            }
        }

        int Px(int value)
        {
            return UiScale.Pixels(value, layoutScale);
        }

        void CenterStartup()
        {
            if (startupCentered)
                return;
            startupCentered = true;
            if (!userPositioned)
                Location = WindowLayoutPlan.Center(WorkingAreaProvider(), Size);
        }

        void DrawBuildResult(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            var item = (BuildMatch)matches.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Theme.Selection : matches.BackColor))
                e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Bounds.Width < Px(460))
            {
                var nameBounds = new Rectangle(e.Bounds.Left + Px(8), e.Bounds.Top, e.Bounds.Width - Px(100), Px(26));
                TextRenderer.DrawText(e.Graphics, item.DisplayKind, matches.Font, nameBounds, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                nameBounds.Y += Px(24);
                TextRenderer.DrawText(e.Graphics, AvailabilityResult.FormatSize(item.SizeBytes), matches.Font, nameBounds, Theme.SecondaryText, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, "Available", matches.Font, new Rectangle(e.Bounds.Right - Px(92), e.Bounds.Top, Px(86), e.Bounds.Height), Theme.Success, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                return;
            }

            var left = new Rectangle(e.Bounds.Left + Px(8), e.Bounds.Top, e.Bounds.Width - Px(246), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, (selected ? "●  " : "○  ") + item.DisplayKind, matches.Font, left, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var sizeBounds = new Rectangle(e.Bounds.Right - Px(244), e.Bounds.Top, Px(128), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, AvailabilityResult.FormatSize(item.SizeBytes), matches.Font, sizeBounds, Theme.SecondaryText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            var right = new Rectangle(e.Bounds.Right - Px(112), e.Bounds.Top, Px(104), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, "Available", matches.Font, right, Theme.Success, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            if ((e.State & DrawItemState.Focus) != 0)
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, ForeColor, matches.BackColor);
        }

        static void ScaleTree(Control parent, float factor)
        {
            UiScale.ApplyTree(parent, factor);
        }
    }
}
