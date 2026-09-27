using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using LeapworkBuildManager;
class DiagnosticLayoutTests
{
    static IEnumerable<Control> All(Control c) { foreach(Control child in c.Controls) { yield return child; foreach(var sub in All(child)) yield return sub; } }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using(var dialog = new HelpDialog(null,"2024.1.486","Ready to download.","https://sawindowreleasedata.blob.core.windows.net/Leapwork_Release_x64_2024.1.486.msi",new DiagnosticEvent[0],"Report"))
        {
            dialog.Show(); dialog.SelectDiagnostics();
            foreach(int width in new[]{660,440,660})
            {
                dialog.FitToArea(new Rectangle(0,0,width,700)); dialog.SelectDiagnostics(); Application.DoEvents();
                var controls=All(dialog).ToArray();
                var reveal=controls.First(c=>c.Text=="Show full URL");
                var copy=controls.First(c=>c.Text=="Copy URL");
                var link=controls.First(c=>c.AccessibleName=="Download URL");
                var a=reveal.RectangleToScreen(reveal.ClientRectangle); var b=copy.RectangleToScreen(copy.ClientRectangle); var l=link.RectangleToScreen(link.ClientRectangle);
                if(a.Top!=b.Top || a.Height!=b.Height || b.Left-a.Right<8 || l.Top-a.Bottom<8 || b.Right>l.Right+2) throw new Exception("Link action spacing failed at "+width);
                if(!dialog.FooterActionsFit) throw new Exception("Footer clipping");
                Console.WriteLine("PASS responsive link actions at "+width);
            }
            dialog.Close();
        }
    }
}
